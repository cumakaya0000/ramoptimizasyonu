using System.Diagnostics;
using System.Management;
using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.Core;

public class ProcessAnalyzer
{
    private readonly SafetyManager _safetyManager;
    private readonly Dictionary<string, double> _cpuCache = new();
    private readonly object _cpuLock = new();

    // Hard-coded known user apps for quicker categorization
    private static readonly HashSet<string> KnownUserApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "opera", "brave", "iexplore",
        "Discord", "Teams", "Slack", "zoom", "skype",
        "Steam", "EpicGamesLauncher", "GOGGalaxy", "Battle.net",
        "OneDrive", "Dropbox", "GoogleDriveFS", "Spotify", "iTunes",
        "VLC", "vlc", "WINWORD", "EXCEL", "POWERPNT", "OUTLOOK",
        "notepad", "notepad++", "7zFM", "WinRAR",
        "AdobeUpdateService", "CreativeCloud", "photoshop",
        "devenv", "Code", "rider64"
    };

    private static readonly HashSet<string> KnownSystemServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "svchost", "lsass", "csrss", "smss", "wininit", "services",
        "system", "registry", "dwm", "winlogon", "audiodg", "fontdrvhost",
        "Memory Compression", "Secure System", "spoolsv", "taskhostw",
        "sihost", "ctfmon", "conhost", "RuntimeBroker", "ShellExperienceHost",
        "StartMenuExperienceHost", "SearchHost", "SearchIndexer",
        "WmiPrvSE", "unsecapp", "dllhost", "msdtc", "lsm"
    };

    public ProcessAnalyzer(SafetyManager safetyManager)
    {
        _safetyManager = safetyManager;
    }

    public async Task<List<ProcessInfoModel>> GetAllProcessesAsync(
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() => GetAllProcesses(progress, ct), ct);
    }

    public List<ProcessInfoModel> GetAllProcesses(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var result = new List<ProcessInfoModel>();
        var allProcesses = Process.GetProcesses();
        int total = allProcesses.Length;
        int done = 0;

        // Pre-fetch CPU usage with a short delay approach using PerformanceCounter
        // We'll use a simpler WMI approach to avoid PerformanceCounter thread issues
        var wmiCpuData = GetWmiCpuData();

        foreach (var proc in allProcesses)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var model = BuildProcessModel(proc, wmiCpuData);
                result.Add(model);
            }
            catch { /* Skip inaccessible processes */ }
            finally
            {
                done++;
                progress?.Report((int)((double)done / total * 100));
                try { proc.Dispose(); } catch { }
            }
        }

        return result.OrderByDescending(p => p.RamUsageBytes).ToList();
    }

    private ProcessInfoModel BuildProcessModel(Process proc, Dictionary<int, double> wmiCpuData)
    {
        var model = new ProcessInfoModel
        {
            Name = proc.ProcessName,
            Pid = proc.Id,
        };

        try { model.RamUsageBytes = proc.WorkingSet64; } catch { }
        try { model.StartTime = proc.StartTime; } catch { }

        // CPU from WMI cache
        if (wmiCpuData.TryGetValue(proc.Id, out double cpu))
            model.CpuUsagePercent = Math.Round(cpu, 1);

        // File path & publisher
        try
        {
            model.FilePath = proc.MainModule?.FileName ?? string.Empty;
            if (!string.IsNullOrEmpty(model.FilePath))
            {
                model.Publisher = GetPublisher(model.FilePath);
                model.IsSignedByMicrosoft = IsMicrosoftSigned(model.FilePath);
            }
        }
        catch { }

        // Classify
        model.IsSystemProcess = IsSystemProcess(proc, model);
        model.IsUserApplication = IsUserApp(proc.ProcessName);
        model.CanTerminate = _safetyManager.CanTerminateProcess(proc.ProcessName);

        ClassifyProcess(model);
        model.OptimizationScore = CalculateOptimizationScore(model);

        return model;
    }

    private static Dictionary<int, double> GetWmiCpuData()
    {
        var result = new Dictionary<int, double>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT IDProcess, PercentProcessorTime FROM Win32_PerfFormattedData_PerfProc_Process");
            foreach (ManagementObject obj in searcher.Get())
            {
                try
                {
                    var pid = Convert.ToInt32(obj["IDProcess"]);
                    var pct = Convert.ToDouble(obj["PercentProcessorTime"]);
                    result[pid] = pct;
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    private static string GetPublisher(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return string.Empty;
            var vi = FileVersionInfo.GetVersionInfo(filePath);
            return vi.CompanyName ?? vi.FileDescription ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static bool IsMicrosoftSigned(string filePath)
    {
        try
        {
            var publisher = string.Empty;
            if (File.Exists(filePath))
            {
                var vi = FileVersionInfo.GetVersionInfo(filePath);
                publisher = (vi.CompanyName ?? string.Empty).ToLowerInvariant();
            }
            return publisher.Contains("microsoft");
        }
        catch { return false; }
    }

    private static bool IsSystemProcess(Process proc, ProcessInfoModel model)
    {
        if (KnownSystemServices.Contains(proc.ProcessName)) return true;
        if (proc.SessionId == 0) return true;
        if (model.IsSignedByMicrosoft && KnownSystemServices.Contains(proc.ProcessName)) return true;
        return false;
    }

    private static bool IsUserApp(string processName)
    {
        return KnownUserApps.Contains(processName);
    }

    private void ClassifyProcess(ProcessInfoModel model)
    {
        if (model.IsSystemProcess || !model.CanTerminate)
        {
            model.Category = ProcessCategory.Green;
            model.Risk = RiskLevel.Critical;
            model.Recommendation = "Sistem processi – dokunmayın.";
            return;
        }

        if (model.IsSignedByMicrosoft)
        {
            model.Category = ProcessCategory.Green;
            model.Risk = RiskLevel.High;
            model.Recommendation = "Microsoft imzalı – güvenli.";
            return;
        }

        if (model.IsUserApplication)
        {
            if (model.RamUsageMB > 300)
            {
                model.Category = ProcessCategory.Yellow;
                model.Risk = RiskLevel.Low;
                model.Recommendation = "Yüksek RAM tüketiyor. Kullanmıyorsanız kapatabilirsiniz.";
            }
            else
            {
                model.Category = ProcessCategory.Green;
                model.Risk = RiskLevel.Low;
                model.Recommendation = "Kullanıcı uygulaması – güvenli.";
            }
            return;
        }

        // Unknown background process using significant RAM
        if (model.RamUsageMB > 150)
        {
            model.Category = ProcessCategory.Red;
            model.Risk = RiskLevel.Medium;
            model.Recommendation = "Bilinmeyen arka plan processi – dikkatli olun.";
        }
        else
        {
            model.Category = ProcessCategory.Yellow;
            model.Risk = RiskLevel.Low;
            model.Recommendation = "Düşük etkili arka plan processi.";
        }
    }

    private static int CalculateOptimizationScore(ProcessInfoModel model)
    {
        if (model.IsSystemProcess || !model.CanTerminate) return 0;
        if (model.IsSignedByMicrosoft) return 5;

        int score = 0;

        // RAM contribution (0-50 pts)
        score += (int)Math.Min(50, model.RamUsageMB / 10);

        // CPU contribution (0-20 pts)
        score += (int)Math.Min(20, model.CpuUsagePercent * 2);

        // User app penalty (if it's a user app the user might want it)
        if (model.IsUserApplication) score -= 10;

        // Boost for unknown processes
        if (!model.IsUserApplication && !model.IsSignedByMicrosoft) score += 20;

        return Math.Max(0, Math.Min(100, score));
    }
}
