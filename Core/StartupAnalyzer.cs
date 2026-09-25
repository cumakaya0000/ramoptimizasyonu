using System.Diagnostics;
using Microsoft.Win32;
using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.Core;

public class StartupAnalyzer
{
    private readonly SafetyManager _safetyManager;

    private static readonly (RegistryKey Hive, string Path, bool IsHKCU)[] RegistryLocations = new[]
    {
        (Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true),
        (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false),
        (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", false),
    };

    public StartupAnalyzer(SafetyManager safetyManager)
    {
        _safetyManager = safetyManager;
    }

    public async Task<List<StartupItemModel>> GetAllStartupItemsAsync(CancellationToken ct = default)
    {
        return await Task.Run(() => GetAllStartupItems(ct), ct);
    }

    public List<StartupItemModel> GetAllStartupItems(CancellationToken ct = default)
    {
        var items = new List<StartupItemModel>();

        // 1. Registry Run keys
        foreach (var (hive, path, isHKCU) in RegistryLocations)
        {
            ct.ThrowIfCancellationRequested();
            items.AddRange(ScanRegistryKey(hive, path, isHKCU));
        }

        // 2. Startup folders
        ct.ThrowIfCancellationRequested();
        items.AddRange(ScanStartupFolders());

        // 3. Task Scheduler (best effort)
        ct.ThrowIfCancellationRequested();
        items.AddRange(ScanTaskScheduler());

        // Remove duplicates by name
        return items
            .GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(i => i.Name)
            .ToList();
    }

    private List<StartupItemModel> ScanRegistryKey(RegistryKey hive, string path, bool isHKCU)
    {
        var items = new List<StartupItemModel>();
        try
        {
            using var key = hive.OpenSubKey(path, writable: false);
            if (key == null) return items;

            foreach (var name in key.GetValueNames())
            {
                try
                {
                    var value = key.GetValue(name)?.ToString() ?? string.Empty;
                    // Extract actual exe path (strip arguments)
                    var exePath = ExtractExePath(value);
                    var model = new StartupItemModel
                    {
                        Name = name,
                        FilePath = exePath,
                        RegistryKey = $"{(isHKCU ? "HKCU" : "HKLM")}\\{path}",
                        StartupType = StartupItemType.Registry,
                        IsEnabled = true,
                        CanDisable = _safetyManager.CanDisableStartup(name),
                        Publisher = GetPublisher(exePath),
                    };
                    ClassifyStartupItem(model);
                    items.Add(model);
                }
                catch { }
            }
        }
        catch { }
        return items;
    }

    private List<StartupItemModel> ScanStartupFolders()
    {
        var items = new List<StartupItemModel>();
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
        };

        foreach (var folder in folders)
        {
            try
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var file in Directory.GetFiles(folder, "*.lnk", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var model = new StartupItemModel
                    {
                        Name = name,
                        FilePath = file,
                        StartupType = StartupItemType.StartupFolder,
                        IsEnabled = true,
                        CanDisable = _safetyManager.CanDisableStartup(name),
                        Publisher = GetPublisher(file),
                    };
                    ClassifyStartupItem(model);
                    items.Add(model);
                }
            }
            catch { }
        }
        return items;
    }

    private List<StartupItemModel> ScanTaskScheduler()
    {
        var items = new List<StartupItemModel>();
        try
        {
            // Use schtasks.exe for compatibility without additional NuGet
            var psi = new ProcessStartInfo("schtasks.exe", "/query /fo CSV /nh")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return items;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10000);

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var cols = ParseCsvLine(line);
                    if (cols.Length < 3) continue;
                    var taskName = cols[0].Trim('"', ' ');
                    var status = cols[2].Trim('"', ' ');

                    if (taskName.StartsWith("\\Microsoft\\", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!status.Contains("Ready", StringComparison.OrdinalIgnoreCase) &&
                        !status.Contains("Running", StringComparison.OrdinalIgnoreCase)) continue;

                    var model = new StartupItemModel
                    {
                        Name = Path.GetFileName(taskName).TrimStart('\\'),
                        FilePath = taskName,
                        StartupType = StartupItemType.TaskScheduler,
                        IsEnabled = true,
                        CanDisable = _safetyManager.CanDisableStartup(taskName),
                        Publisher = string.Empty,
                        ImpactLevel = "LOW"
                    };
                    items.Add(model);
                }
                catch { }
            }
        }
        catch { }
        return items;
    }

    private static string[] ParseCsvLine(string line)
    {
        return line.Split(',');
    }

    private static string ExtractExePath(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return string.Empty;
        commandLine = commandLine.Trim();
        if (commandLine.StartsWith("\""))
        {
            var end = commandLine.IndexOf('"', 1);
            if (end > 0) return commandLine[1..end];
        }
        var spaceIdx = commandLine.IndexOf(' ');
        return spaceIdx > 0 ? commandLine[..spaceIdx] : commandLine;
    }

    private static string GetPublisher(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return string.Empty;
            var vi = FileVersionInfo.GetVersionInfo(filePath);
            return vi.CompanyName ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static void ClassifyStartupItem(StartupItemModel model)
    {
        var publisher = (model.Publisher ?? string.Empty).ToLowerInvariant();
        var name = (model.Name ?? string.Empty).ToLowerInvariant();

        // Microsoft / Windows items
        if (publisher.Contains("microsoft") || name.Contains("windows"))
        {
            model.Risk = RiskLevel.Medium;
            model.ImpactLevel = "LOW";
            model.Description = "Microsoft / Windows bileşeni.";
            model.OptimizationScore = 10;
            return;
        }

        // Common high-impact apps
        if (name.Contains("steam") || name.Contains("epic") || name.Contains("discord") ||
            name.Contains("spotify") || name.Contains("teams") || name.Contains("skype") ||
            name.Contains("zoom") || name.Contains("slack") || name.Contains("onedrive") ||
            name.Contains("adobe") || name.Contains("dropbox") || name.Contains("google"))
        {
            model.Risk = RiskLevel.Low;
            model.ImpactLevel = "HIGH";
            model.Description = "Sık kullanılan uygulama. Başlangıçta gereksiz yere yavaşlatabilir.";
            model.OptimizationScore = 70;
            return;
        }

        model.Risk = RiskLevel.Low;
        model.ImpactLevel = "MEDIUM";
        model.Description = "Üçüncü parti başlangıç programı.";
        model.OptimizationScore = 40;
    }
}
