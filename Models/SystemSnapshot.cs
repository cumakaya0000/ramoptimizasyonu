namespace WinRamOptimizer.Models;

/// <summary>
/// System snapshot taken before optimization for rollback support.
/// </summary>
public class SystemSnapshot
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Description { get; set; } = string.Empty;

    // RAM info at snapshot time
    public long TotalRamBytes { get; set; }
    public long UsedRamBytes { get; set; }

    // Services snapshot: serviceName -> startType
    public Dictionary<string, string> ServiceStartTypes { get; set; } = new();
    // Services that were running at snapshot time
    public List<string> RunningServices { get; set; } = new();

    // Startup registry entries: keyPath+name -> value
    public Dictionary<string, string> StartupRegistryEntries { get; set; } = new();
    // Disabled startup entries (by name)
    public List<string> DisabledStartupEntries { get; set; } = new();

    // Running processes at snapshot time
    public List<string> RunningProcessNames { get; set; } = new();

    // Applied changes (for undo)
    public List<AppliedChange> AppliedChanges { get; set; } = new();

    public string FileName => $"backup-{CreatedAt:yyyy-MM-dd-HHmm}.json";
}

public class AppliedChange
{
    public string ChangeType { get; set; } = string.Empty; // "ServiceStopped", "ServiceStartTypeChanged", "StartupDisabled", "ProcessTerminated"
    public string TargetName { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class RamInfo
{
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long FreeBytes { get; set; }
    public double UsagePercent { get; set; }

    public double TotalGB => TotalBytes / (1024.0 * 1024.0 * 1024.0);
    public double UsedGB => UsedBytes / (1024.0 * 1024.0 * 1024.0);
    public double FreeGB => FreeBytes / (1024.0 * 1024.0 * 1024.0);
    public double TotalMB => TotalBytes / (1024.0 * 1024.0);
    public double UsedMB => UsedBytes / (1024.0 * 1024.0);
    public double FreeMB => FreeBytes / (1024.0 * 1024.0);
}

public enum SystemProfile
{
    Gaming,
    Office,
    Developer,
    LowRamPC,
    MaximumPerformance,
    Custom
}
