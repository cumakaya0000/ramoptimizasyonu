using System.ServiceProcess;
using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.Core;

/// <summary>
/// Executes optimization actions with full safety checks, backup, and logging.
/// </summary>
public class OptimizationEngine
{
    private readonly SafetyManager _safetyManager;
    private readonly ProcessManager _processManager;
    private readonly WindowsServiceManager _serviceManager;
    private readonly StartupManager _startupManager;
    private readonly RestorePointService _restoreService;

    public OptimizationEngine(SafetyManager safetyManager)
    {
        _safetyManager = safetyManager;
        _processManager = new ProcessManager(safetyManager);
        _serviceManager = new WindowsServiceManager(safetyManager);
        _startupManager = new StartupManager(safetyManager);
        _restoreService = new RestorePointService();
    }

    /// <summary>
    /// Builds smart optimization suggestions from analysis results.
    /// </summary>
    public List<OptimizationSuggestion> BuildSuggestions(
        SystemAnalysisResult analysis,
        SystemProfile profile = SystemProfile.Office)
    {
        var suggestions = new List<OptimizationSuggestion>();

        // Processes: Red and Yellow, can terminate, not system
        foreach (var proc in analysis.Processes
            .Where(p => (p.Category == ProcessCategory.Red || p.Category == ProcessCategory.Yellow)
                     && p.CanTerminate
                     && p.OptimizationScore >= 30)
            .Take(15))
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Name = proc.Name,
                Type = SuggestionType.TerminateProcess,
                Description = $"Process – {proc.Publisher}",
                EstimatedRamSaveBytes = proc.RamUsageBytes,
                Risk = proc.Risk,
                Score = proc.OptimizationScore,
                ProcessPid = proc.Pid,
                IsAutoSelected = proc.Risk == RiskLevel.Low && proc.OptimizationScore >= 50
            });
        }

        // Services: Safe/Optional and Running
        foreach (var svc in analysis.Services
            .Where(s => (s.Category == ServiceCategory.Safe || s.Category == ServiceCategory.Optional)
                     && s.Status == "Running"
                     && s.CanDisable)
            .Take(10))
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Name = svc.ServiceName,
                DisplayName = svc.DisplayName,
                Type = SuggestionType.StopService,
                Description = svc.Description,
                EstimatedRamSaveBytes = svc.EstimatedRamBytes,
                Risk = svc.Risk,
                Score = svc.Category == ServiceCategory.Safe ? 60 : 40,
                IsAutoSelected = svc.Risk == RiskLevel.Low && svc.Category == ServiceCategory.Safe
            });
        }

        // Startup: High-impact enabled items
        foreach (var startup in analysis.StartupItems
            .Where(s => s.IsEnabled && s.CanDisable && s.ImpactLevel == "HIGH")
            .Take(10))
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Name = startup.Name,
                Type = SuggestionType.DisableStartup,
                Description = $"Başlangıç programı – {startup.Publisher}",
                EstimatedRamSaveBytes = 0,
                Risk = startup.Risk,
                Score = startup.OptimizationScore,
                RegistryKey = startup.RegistryKey,
                FilePath = startup.FilePath,
                IsAutoSelected = startup.Risk == RiskLevel.Low
            });
        }

        // Don't auto-select HIGH or CRITICAL risk items
        foreach (var s in suggestions.Where(s => s.Risk >= RiskLevel.High))
            s.IsAutoSelected = false;

        return suggestions.OrderByDescending(s => s.EstimatedRamSaveBytes).ToList();
    }

    /// <summary>
    /// Executes the selected optimization suggestions.
    /// </summary>
    public async Task<OptimizationResult> ExecuteAsync(
        List<OptimizationSuggestion> selectedSuggestions,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var result = new OptimizationResult();
        LogService.LogOptimizationStart();

        // Create application snapshot
        var snapshot = _restoreService.CreateSnapshot("Pre-optimization");
        _restoreService.SaveSnapshot(snapshot, out var backupPath);
        result.BackupFilePath = backupPath;

        var ramBefore = Services.MemoryService.GetRamInfo();
        result.RamBeforeBytes = ramBefore.UsedBytes;

        foreach (var suggestion in selectedSuggestions)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                switch (suggestion.Type)
                {
                    case SuggestionType.TerminateProcess:
                        progress?.Report($"Process sonlandırılıyor: {suggestion.Name}");
                        if (_processManager.TerminateProcess(
                            suggestion.ProcessPid, suggestion.Name,
                            suggestion.EstimatedRamSaveBytes, out var procError))
                        {
                            result.ProcessesTerminated++;
                            result.TerminatedProcessNames.Add(suggestion.Name);
                            result.RamSavedBytes += suggestion.EstimatedRamSaveBytes;
                            snapshot.AppliedChanges.Add(new AppliedChange
                            {
                                ChangeType = "ProcessTerminated",
                                TargetName = suggestion.Name,
                                OldValue = "Running",
                                NewValue = "Terminated"
                            });
                        }
                        else
                        {
                            result.Errors.Add($"{suggestion.Name}: {procError}");
                        }
                        break;

                    case SuggestionType.StopService:
                        progress?.Report($"Servis durduruluyor: {suggestion.Name}");
                        var oldStartType = _serviceManager.GetCurrentStartType(suggestion.Name);

                        if (_serviceManager.StopService(suggestion.Name, out var svcError))
                        {
                            result.ServicesStopped++;
                            result.StoppedServiceNames.Add(suggestion.Name);
                            result.RamSavedBytes += suggestion.EstimatedRamSaveBytes;

                            // Also set to Manual if it's Automatic
                            if (oldStartType == "Auto" || oldStartType == "Automatic")
                            {
                                _serviceManager.SetServiceStartType(
                                    suggestion.Name, ServiceStartMode.Manual, out _);
                            }

                            snapshot.AppliedChanges.Add(new AppliedChange
                            {
                                ChangeType = "ServiceStopped",
                                TargetName = suggestion.Name,
                                OldValue = "Running",
                                NewValue = "Stopped"
                            });
                        }
                        else
                        {
                            result.Errors.Add($"{suggestion.Name}: {svcError}");
                        }
                        break;

                    case SuggestionType.DisableStartup:
                        progress?.Report($"Başlangıç devre dışı: {suggestion.Name}");
                        bool isHKCU = suggestion.RegistryKey.StartsWith("HKCU");
                        var keyPath = suggestion.RegistryKey.Contains("\\")
                            ? suggestion.RegistryKey[(suggestion.RegistryKey.IndexOf('\\') + 1)..]
                            : suggestion.RegistryKey;

                        if (_startupManager.DisableStartupEntry(suggestion.Name, keyPath, isHKCU, out var startupError))
                        {
                            result.StartupItemsDisabled++;
                            result.DisabledStartupNames.Add(suggestion.Name);
                            snapshot.AppliedChanges.Add(new AppliedChange
                            {
                                ChangeType = "StartupDisabled",
                                TargetName = suggestion.Name,
                                OldValue = keyPath,
                                NewValue = "Disabled"
                            });
                        }
                        else
                        {
                            result.Errors.Add($"{suggestion.Name}: {startupError}");
                        }
                        break;
                }

                await Task.Delay(100, ct); // Small delay between operations
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{suggestion.Name}: {ex.Message}");
                LogService.Error($"Optimization step failed for {suggestion.Name}", ex);
            }
        }

        // Save updated snapshot with applied changes
        _restoreService.SaveSnapshot(snapshot, out _);

        var ramAfter = Services.MemoryService.GetRamInfo();
        result.RamAfterBytes = ramAfter.UsedBytes;
        // Use measured difference if larger than estimated
        var measured = result.RamBeforeBytes - result.RamAfterBytes;
        if (measured > result.RamSavedBytes) result.RamSavedBytes = measured;

        result.Success = result.Errors.Count == 0;
        LogService.LogOptimizationEnd(result.RamSavedBytes);
        return result;
    }

    public async Task<(bool Success, List<string> Errors)> RestoreLastSnapshotAsync()
    {
        var snapshots = _restoreService.GetAllSnapshots();
        if (!snapshots.Any())
        {
            return (false, new List<string> { "Geri alınacak snapshot bulunamadı." });
        }

        var latest = snapshots.OrderByDescending(s => s.CreatedAt).First();
        List<string> errors = new();
        bool success = await Task.Run(() => _restoreService.RestoreSnapshot(latest, out errors));
        return (success, errors);
    }

    public List<SystemSnapshot> GetAvailableSnapshots() => _restoreService.GetAllSnapshots();

    public bool CreateWindowsRestorePoint(out string error) =>
        _restoreService.CreateWindowsRestorePoint("WinRam Optimizer – Pre-optimization", out error);
}

public class OptimizationSuggestion
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public SuggestionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public long EstimatedRamSaveBytes { get; set; }
    public RiskLevel Risk { get; set; }
    public int Score { get; set; }
    public bool IsAutoSelected { get; set; }
    public bool IsSelected { get; set; }

    // Process-specific
    public int ProcessPid { get; set; }

    // Startup-specific
    public string RegistryKey { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;

    public double EstimatedRamSaveMB => EstimatedRamSaveBytes / (1024.0 * 1024.0);

    public string EstimatedRamDisplay
    {
        get
        {
            if (EstimatedRamSaveBytes == 0) return "–";
            if (EstimatedRamSaveMB >= 1024)
                return $"{EstimatedRamSaveBytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
            return $"{EstimatedRamSaveMB:F0} MB";
        }
    }

    public string RiskDisplay => Risk switch
    {
        RiskLevel.Low => "LOW",
        RiskLevel.Medium => "MEDIUM",
        RiskLevel.High => "HIGH",
        RiskLevel.Critical => "CRITICAL",
        _ => "UNKNOWN"
    };

    public string TypeDisplay => Type switch
    {
        SuggestionType.TerminateProcess => "Process",
        SuggestionType.StopService => "Servis",
        SuggestionType.DisableStartup => "Başlangıç",
        _ => "Bilinmiyor"
    };
}

public enum SuggestionType
{
    TerminateProcess,
    StopService,
    DisableStartup
}
