using Newtonsoft.Json;

namespace WinRamOptimizer.Core;

/// <summary>
/// Central safety gate. Every destructive action MUST pass through this class.
/// </summary>
public class SafetyManager
{
    private readonly HashSet<string> _protectedServices;
    private readonly HashSet<string> _protectedProcesses;
    private readonly HashSet<string> _safeServiceNames;

    public SafetyManager()
    {
        _protectedServices = LoadJsonSet("Config/protected-services.json");
        _safeServiceNames = LoadSafeServiceNames("Config/safe-services.json");
        _protectedProcesses = LoadProtectedProcesses("Config/process-rules.json");
    }

    private static HashSet<string> LoadJsonSet(string relativePath)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            if (!File.Exists(fullPath)) return set;
            var json = File.ReadAllText(fullPath);
            var list = JsonConvert.DeserializeObject<List<string>>(json);
            if (list != null) foreach (var item in list) set.Add(item);
        }
        catch (Exception ex)
        {
            Services.LogService.Error($"SafetyManager: Failed to load {relativePath}", ex);
        }
        return set;
    }

    private static HashSet<string> LoadSafeServiceNames(string relativePath)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            if (!File.Exists(fullPath)) return set;
            var json = File.ReadAllText(fullPath);
            var list = JsonConvert.DeserializeObject<List<dynamic>>(json);
            if (list == null) return set;
            foreach (var item in list)
            {
                string? svc = item.ServiceName?.ToString();
                if (!string.IsNullOrEmpty(svc)) set.Add(svc);
            }
        }
        catch { }
        return set;
    }

    private static HashSet<string> LoadProtectedProcesses(string relativePath)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Hard-coded critical processes always protected regardless of config
            "System", "Registry", "smss", "csrss", "wininit", "services",
            "lsass", "svchost", "winlogon", "dwm", "explorer", "audiodg",
            "fontdrvhost", "Memory Compression", "Secure System", "MsMpEng",
            "NisSrv", "SecurityHealthSystray", "spoolsv", "taskhostw",
            "sihost", "ctfmon", "conhost", "RuntimeBroker", "ShellExperienceHost",
            "StartMenuExperienceHost", "SearchHost", "SearchIndexer", "WmiPrvSE",
            "unsecapp", "dllhost", "msdtc", "lsm", "wlanext"
        };

        try
        {
            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            if (!File.Exists(fullPath)) return set;
            var json = File.ReadAllText(fullPath);
            var rules = JsonConvert.DeserializeObject<dynamic>(json);
            if (rules?.ProtectedProcesses != null)
            {
                foreach (var p in rules.ProtectedProcesses)
                    set.Add((string)p);
            }
        }
        catch { }
        return set;
    }

    /// <summary>
    /// Returns true if the process can be safely terminated by the optimizer.
    /// </summary>
    public bool CanTerminateProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;

        // Strip .exe if present
        var name = Path.GetFileNameWithoutExtension(processName);

        if (_protectedProcesses.Contains(name)) return false;
        if (_protectedProcesses.Contains(processName)) return false;

        // Never kill session 0 process names that look like Windows internals
        var forbidden = new[] { "svchost", "lsass", "csrss", "smss", "wininit", "services",
                                 "system", "registry", "dwm", "winlogon" };
        foreach (var f in forbidden)
            if (name.Equals(f, StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    /// <summary>
    /// Returns true if the service can be stopped/disabled by the optimizer.
    /// </summary>
    public bool CanDisableService(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName)) return false;
        if (_protectedServices.Contains(serviceName)) return false;

        // Extra hard-coded critical service names
        var critical = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RpcSs", "PlugPlay", "Dhcp", "Dnscache", "EventLog",
            "AudioSrv", "AudioEndpointBuilder", "WinDefend", "mpssvc",
            "MpsSvc", "Schedule", "BFE", "Winmgmt", "CryptSvc",
            "LanmanServer", "LanmanWorkstation", "NlaSvc", "netprofm",
            "nsi", "WlanSvc", "DcomLaunch", "LSM", "SamSs", "lsass",
            "Power", "gpsvc", "wscsvc", "UserManager", "ProfSvc",
            "SessionEnv", "TermService", "wuauserv", "WaaSMedicSvc",
            "Tcpip", "Tcpip6", "AFD", "NetBT"
        };

        if (critical.Contains(serviceName)) return false;

        return true;
    }

    /// <summary>
    /// Returns true if a startup item can be disabled.
    /// </summary>
    public bool CanDisableStartup(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Windows Defender", "SecurityHealth", "MsMpEng", "Windows Security",
            "explorer", "ctfmon", "InputPersonalization"
        };

        return !forbidden.Contains(name);
    }

    /// <summary>
    /// Returns whether a service is in the known-safe-to-disable list.
    /// </summary>
    public bool IsKnownSafeService(string serviceName) => _safeServiceNames.Contains(serviceName);

    /// <summary>
    /// Returns true if the service is on the protected (never touch) list.
    /// </summary>
    public bool IsProtectedService(string serviceName) => _protectedServices.Contains(serviceName) || !CanDisableService(serviceName);
}
