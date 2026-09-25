using WinRamOptimizer.Models;
using Newtonsoft.Json;
using System.ServiceProcess;

namespace WinRamOptimizer.Services;

/// <summary>
/// Creates and restores system snapshots for rollback support.
/// </summary>
public class RestorePointService
{
    private readonly string _backupDirectory;

    public RestorePointService()
    {
        _backupDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
        Directory.CreateDirectory(_backupDirectory);
    }

    public SystemSnapshot CreateSnapshot(string description = "Pre-optimization snapshot")
    {
        var snapshot = new SystemSnapshot
        {
            Description = description,
            CreatedAt = DateTime.Now
        };

        // RAM info
        var ramInfo = MemoryService.GetRamInfo();
        snapshot.TotalRamBytes = ramInfo.TotalBytes;
        snapshot.UsedRamBytes = ramInfo.UsedBytes;

        // Running processes
        try
        {
            snapshot.RunningProcessNames = System.Diagnostics.Process.GetProcesses()
                .Select(p => { try { return p.ProcessName; } catch { return string.Empty; } })
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToList();
        }
        catch (Exception ex) { LogService.Error("Snapshot: Failed to capture processes", ex); }

        // Services
        try
        {
            foreach (var sc in ServiceController.GetServices())
            {
                try
                {
                    snapshot.ServiceStartTypes[sc.ServiceName] = sc.StartType.ToString();
                    if (sc.Status == ServiceControllerStatus.Running)
                        snapshot.RunningServices.Add(sc.ServiceName);
                }
                catch { }
            }
        }
        catch (Exception ex) { LogService.Error("Snapshot: Failed to capture services", ex); }

        // Startup registry entries
        CaptureStartupRegistry(snapshot);

        LogService.Info($"System snapshot created: {snapshot.FileName}");
        return snapshot;
    }

    private static void CaptureStartupRegistry(SystemSnapshot snapshot)
    {
        var keys = new (Microsoft.Win32.RegistryKey hive, string path, bool isHKCU)[]
        {
            (Microsoft.Win32.Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true),
            (Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false),
            (Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", false),
        };

        foreach (var (hive, path, _) in keys)
        {
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key == null) continue;
                foreach (var name in key.GetValueNames())
                {
                    var val = key.GetValue(name)?.ToString() ?? string.Empty;
                    snapshot.StartupRegistryEntries[$"{path}\\{name}"] = val;
                }
            }
            catch { }
        }
    }

    public bool SaveSnapshot(SystemSnapshot snapshot, out string filePath)
    {
        filePath = string.Empty;
        try
        {
            filePath = Path.Combine(_backupDirectory, snapshot.FileName);
            var json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
            File.WriteAllText(filePath, json);
            LogService.Info($"Snapshot saved to: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to save snapshot", ex);
            return false;
        }
    }

    public List<SystemSnapshot> GetAllSnapshots()
    {
        var snapshots = new List<SystemSnapshot>();
        try
        {
            foreach (var file in Directory.GetFiles(_backupDirectory, "backup-*.json").OrderByDescending(f => f))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var snapshot = JsonConvert.DeserializeObject<SystemSnapshot>(json);
                    if (snapshot != null) snapshots.Add(snapshot);
                }
                catch { }
            }
        }
        catch { }
        return snapshots;
    }

    public bool RestoreSnapshot(SystemSnapshot snapshot, out List<string> errors)
    {
        errors = new List<string>();
        bool allOk = true;

        LogService.Info($"Starting restore from snapshot: {snapshot.FileName}");

        // Restore services
        foreach (var change in snapshot.AppliedChanges)
        {
            try
            {
                if (change.ChangeType == "ServiceStopped")
                {
                    // Try to start the service again
                    using var sc = new ServiceController(change.TargetName);
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                        LogService.Info($"Restore: Service restarted: {change.TargetName}");
                    }
                }
                else if (change.ChangeType == "ServiceStartTypeChanged")
                {
                    // Restore original start type
                    var psi = new System.Diagnostics.ProcessStartInfo("sc.exe",
                        $"config \"{change.TargetName}\" start= {MapStartTypeToSc(change.OldValue)}")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = System.Diagnostics.Process.Start(psi);
                    p?.WaitForExit(10000);
                    LogService.Info($"Restore: Service start type restored: {change.TargetName} -> {change.OldValue}");
                }
                else if (change.ChangeType == "StartupDisabled")
                {
                    // Re-enable startup entry from disabled key
                    RestoreStartupEntry(change.TargetName, change.OldValue, errors);
                }
            }
            catch (Exception ex)
            {
                var msg = $"Restore failed for {change.TargetName}: {ex.Message}";
                errors.Add(msg);
                LogService.Error(msg);
                allOk = false;
            }
        }

        return allOk;
    }

    private static void RestoreStartupEntry(string name, string registryKeyPath, List<string> errors)
    {
        try
        {
            var disabledPath = registryKeyPath + @"\AutorunsDisabled";
            // Determine hive from path
            bool isHKCU = registryKeyPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase)
                       || !registryKeyPath.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase);
            var hive = isHKCU ? Microsoft.Win32.Registry.CurrentUser : Microsoft.Win32.Registry.LocalMachine;

            using var disabledKey = hive.OpenSubKey(disabledPath, writable: true);
            if (disabledKey == null) return;

            var val = disabledKey.GetValue(name);
            if (val == null) return;

            using var runKey = hive.OpenSubKey(registryKeyPath, writable: true);
            runKey?.SetValue(name, val);
            disabledKey.DeleteValue(name);
        }
        catch (Exception ex)
        {
            errors.Add($"Startup restore failed for {name}: {ex.Message}");
        }
    }

    private static string MapStartTypeToSc(string startType) => startType.ToLower() switch
    {
        "automatic" => "auto",
        "manual" => "demand",
        "disabled" => "disabled",
        "automaticdelayedstart" => "delayed-auto",
        _ => "demand"
    };

    public string GetBackupDirectory() => _backupDirectory;

    /// <summary>
    /// Creates a Windows System Restore Point using WMI.
    /// </summary>
    public bool CreateWindowsRestorePoint(string description, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            var scope = new System.Management.ManagementScope(@"\\localhost\root\default");
            using var classObj = new System.Management.ManagementClass(scope,
                new System.Management.ManagementPath("SystemRestore"), null);

            var inParams = classObj.GetMethodParameters("CreateRestorePoint");
            inParams["Description"] = description;
            inParams["RestorePointType"] = 12; // MODIFY_SETTINGS
            inParams["EventType"] = 100; // BEGIN_SYSTEM_CHANGE

            var outParams = classObj.InvokeMethod("CreateRestorePoint", inParams, null);
            var returnValue = Convert.ToInt32(outParams["ReturnValue"]);

            if (returnValue == 0)
            {
                LogService.Info($"Windows Restore Point created: {description}");
                return true;
            }
            else
            {
                errorMessage = $"Restore Point oluşturulamadı. Hata kodu: {returnValue}";
                LogService.Warning(errorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Restore Point oluşturulurken hata: {ex.Message}";
            LogService.Error("CreateWindowsRestorePoint failed", ex);
            return false;
        }
    }
}
