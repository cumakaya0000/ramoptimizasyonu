namespace WinRamOptimizer.Models;

public class OptimizationResult
{
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // RAM stats before cleanup
    public long RamBeforeBytes { get; set; }
    public double UsagePercentBefore { get; set; }

    // RAM stats after cleanup & wait
    public long RamAfterBytes { get; set; }
    public double UsagePercentAfter { get; set; }

    // Real gain
    public long RamSavedBytes { get; set; }

    // Counts
    public int ProcessesTerminated { get; set; }
    public int ServicesStopped { get; set; }
    public int ServicesSetToManual { get; set; }
    public int StartupItemsDisabled { get; set; }
    public int ScheduledTasksDisabled { get; set; }
    public int RestartedProcessCount { get; set; }

    public List<string> TerminatedProcessNames { get; set; } = new();
    public List<string> StoppedServiceNames { get; set; } = new();
    public List<string> DisabledStartupNames { get; set; } = new();
    public List<string> DisabledTaskNames { get; set; } = new();
    public List<string> RestartedProcessNames { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    public bool Success { get; set; }
    public string BackupFilePath { get; set; } = string.Empty;

    public double RamBeforeMB => RamBeforeBytes / (1024.0 * 1024.0);
    public double RamAfterMB => RamAfterBytes / (1024.0 * 1024.0);
    public double RamSavedMB => RamSavedBytes / (1024.0 * 1024.0);
    public double RamBeforeGB => RamBeforeBytes / (1024.0 * 1024.0 * 1024.0);
    public double RamAfterGB => RamAfterBytes / (1024.0 * 1024.0 * 1024.0);
    public double RamSavedGB => RamSavedBytes / (1024.0 * 1024.0 * 1024.0);

    public string RamSavedDisplay
    {
        get
        {
            if (RamSavedMB >= 1024)
                return $"{RamSavedGB:F2} GB";
            return $"{RamSavedMB:F0} MB";
        }
    }
}
