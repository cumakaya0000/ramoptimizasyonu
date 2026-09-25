using System.ServiceProcess;
using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.Core;

public enum OptimizationMode
{
    Safe,
    Aggressive,
    Manual
}

public class OptimizationProgress
{
    public string Message { get; set; } = string.Empty;
    public int Percent { get; set; }
}

/// <summary>
/// Executes optimization actions with full safety checks, backup, restart detection and logging.
/// </summary>
public class OptimizationEngine
{
    private readonly SafetyManager _safetyManager;
    private readonly ProcessManager _processManager;
    private readonly WindowsServiceManager _serviceManager;
    private readonly StartupManager _startupManager;
    private readonly ScheduledTaskManager _taskManager;
    private readonly RestorePointService _restoreService;
    private readonly ProcessRestartDetector _restartDetector;

    public OptimizationEngine(SafetyManager safetyManager)
    {
        _safetyManager = safetyManager;
        _processManager = new ProcessManager(safetyManager);
        _serviceManager = new WindowsServiceManager(safetyManager);
        _startupManager = new StartupManager(safetyManager);
        _taskManager = new ScheduledTaskManager(safetyManager);
        _restoreService = new RestorePointService();
        _restartDetector = new ProcessRestartDetector();
    }

    /// <summary>
    /// Builds optimization suggestions from analysis results without list truncation limits.
    /// </summary>
    public List<OptimizationSuggestion> BuildSuggestions(
        SystemAnalysisResult analysis,
        OptimizationMode mode = OptimizationMode.Safe,
        SystemProfile profile = SystemProfile.Office)
    {
        var suggestions = new List<OptimizationSuggestion>();

        // 1. Processes (No .Take() limit)
        foreach (var proc in analysis.Processes
            .Where(p => p.CanTerminate && !p.IsSystemProcess && !p.IsActiveWindow))
        {
            bool isAutoSelect = mode switch
            {
                OptimizationMode.Safe => proc.Risk == RiskLevel.Low && (proc.Category == ProcessCategory.Red || proc.AppCategory == ApplicationCategory.Updater),
                OptimizationMode.Aggressive => proc.Risk == RiskLevel.Low || proc.Risk == RiskLevel.Medium,
                _ => false
            };

            // ACTIVE foreground applications are NEVER auto-selected in any mode
            if (proc.IsActiveWindow) isAutoSelect = false;

            suggestions.Add(new OptimizationSuggestion
            {
                Name = proc.Name,
                DisplayName = $"{proc.Name} (PID: {proc.Pid})",
                Type = SuggestionType.TerminateProcess,
                Description = $"{proc.AppCategoryDisplay} – {proc.Publisher}",
                EstimatedRamSaveBytes = proc.RamUsageBytes,
                Risk = proc.Risk,
                Score = proc.OptimizationScore,
                ProcessPid = proc.Pid,
                IsAutoSelected = isAutoSelect
            });
        }

        // 2. Services (Stop or Set to Manual)
        foreach (var svc in analysis.Services
            .Where(s => s.Status == "Running" && s.CanDisable && !s.IsProtected && !s.IsHardwareService))
        {
            bool isAutoSelect = mode switch
            {
                OptimizationMode.Safe => svc.Category == ServiceCategory.Safe && svc.Risk == RiskLevel.Low,
                OptimizationMode.Aggressive => (svc.Category == ServiceCategory.Safe || svc.Category == ServiceCategory.Optional) &&
                                               (svc.Risk == RiskLevel.Low || svc.Risk == RiskLevel.Medium),
                _ => false
            };

            // Stop service suggestion
            suggestions.Add(new OptimizationSuggestion
            {
                Name = svc.ServiceName,
                DisplayName = svc.DisplayName,
                Type = SuggestionType.StopService,
                Description = svc.Description,
                EstimatedRamSaveBytes = svc.EstimatedRamBytes,
                Risk = svc.Risk,
                Score = svc.Category == ServiceCategory.Safe ? 60 : 40,
                IsAutoSelected = isAutoSelect
            });

            // In Aggressive mode, suggest setting Automatic optional services to Manual
            if (mode == OptimizationMode.Aggressive && (svc.StartType == "Auto" || svc.StartType == "Automatic"))
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    Name = svc.ServiceName,
                    DisplayName = $"{svc.DisplayName} [Manuel Tipe Al]",
                    Type = SuggestionType.SetServiceManual,
                    Description = "Servis başlangıç türü Manuel yapılacaktır.",
                    EstimatedRamSaveBytes = 0,
                    Risk = svc.Risk,
                    Score = 50,
                    IsAutoSelected = svc.Risk == RiskLevel.Low
                });
            }
        }

        // 3. Startup items (No limit)
        foreach (var startup in analysis.StartupItems
            .Where(s => s.IsEnabled && s.CanDisable))
        {
            bool isAutoSelect = mode switch
            {
                OptimizationMode.Safe => startup.Risk == RiskLevel.Low && startup.ImpactLevel == "HIGH",
                OptimizationMode.Aggressive => startup.Risk == RiskLevel.Low || startup.Risk == RiskLevel.Medium,
                _ => false
            };

            suggestions.Add(new OptimizationSuggestion
            {
                Name = startup.Name,
                DisplayName = startup.Name,
                Type = SuggestionType.DisableStartup,
                Description = $"Başlangıç programı – {startup.Publisher}",
                EstimatedRamSaveBytes = 0,
                Risk = startup.Risk,
                Score = startup.OptimizationScore,
                RegistryKey = startup.RegistryKey,
                FilePath = startup.FilePath,
                IsAutoSelected = isAutoSelect
            });
        }

        // 4. Scheduled Tasks (No limit)
        foreach (var task in analysis.ScheduledTasks
            .Where(t => t.IsEnabled && t.CanDisable && !t.IsMicrosoftTask))
        {
            bool isAutoSelect = mode switch
            {
                OptimizationMode.Safe => task.Category == ScheduledTaskCategory.Updater || task.Category == ScheduledTaskCategory.Telemetry,
                OptimizationMode.Aggressive => task.Risk == RiskLevel.Low || task.Risk == RiskLevel.Medium,
                _ => false
            };

            suggestions.Add(new OptimizationSuggestion
            {
                Name = task.TaskPath,
                DisplayName = task.TaskName,
                Type = SuggestionType.DisableScheduledTask,
                Description = task.Description,
                EstimatedRamSaveBytes = 0,
                Risk = task.Risk,
                Score = task.OptimizationScore,
                IsAutoSelected = isAutoSelect
            });
        }

        // Safety enforcement: HIGH, CRITICAL, and UNKNOWN are NEVER auto-selected in any mode
        foreach (var s in suggestions.Where(s => s.Risk >= RiskLevel.High))
            s.IsAutoSelected = false;

        return suggestions.OrderByDescending(s => s.EstimatedRamSaveBytes).ToList();
    }

    /// <summary>
    /// Executes batch optimization asynchronously with runtime safety re-checks and post-cleanup re-analysis.
    /// </summary>
    public async Task<OptimizationResult> ExecuteBatchAsync(
        IEnumerable<OptimizationSuggestion> suggestions,
        OptimizationMode mode,
        IProgress<OptimizationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var selectedList = suggestions.Where(s => s.IsSelected || s.IsAutoSelected).ToList();
        var result = new OptimizationResult();
        LogService.LogOptimizationStart();

        // 1. Snapshot
        progress?.Report(new OptimizationProgress { Message = "Sistem snapshot yedeği oluşturuluyor...", Percent = 5 });
        var snapshot = _restoreService.CreateSnapshot($"Pre-optimization ({mode} mode)");
        _restoreService.SaveSnapshot(snapshot, out var backupPath);
        result.BackupFilePath = backupPath;

        var ramBefore = Services.MemoryService.GetRamInfo();
        result.RamBeforeBytes = ramBefore.UsedBytes;
        result.UsagePercentBefore = ramBefore.UsagePercent;

        int totalCount = selectedList.Count;
        int currentIdx = 0;

        // 2. Batch Execution with Runtime Safety Re-verification (Requirement 17)
        foreach (var sug in selectedList)
        {
            ct.ThrowIfCancellationRequested();
            currentIdx++;
            int pct = 10 + (int)((double)currentIdx / Math.Max(1, totalCount) * 75);

            try
            {
                switch (sug.Type)
                {
                    case SuggestionType.TerminateProcess:
                        progress?.Report(new OptimizationProgress { Message = $"Process kapatılıyor: {sug.Name}", Percent = pct });

                        // Runtime Safety Re-check
                        if (!_safetyManager.CanTerminateProcess(sug.Name, sug.ProcessPid))
                        {
                            result.Errors.Add($"{sug.Name}: SafetyManager işlemi engelledi.");
                            break;
                        }

                        if (_processManager.TerminateProcess(sug.ProcessPid, sug.Name, sug.EstimatedRamSaveBytes, out var procErr))
                        {
                            result.ProcessesTerminated++;
                            result.TerminatedProcessNames.Add(sug.Name);
                            snapshot.AppliedChanges.Add(new AppliedChange
                            {
                                ChangeType = "ProcessTerminated",
                                TargetName = sug.Name,
                                OldValue = "Running",
                                NewValue = "Terminated"
                            });
                        }
                        else if (!string.IsNullOrEmpty(procErr))
                        {
                            result.Errors.Add($"{sug.Name}: {procErr}");
                        }
                        break;

                    case SuggestionType.StopService:
                        progress?.Report(new OptimizationProgress { Message = $"Servis durduruluyor: {sug.Name}", Percent = pct });

                        // Runtime Safety Re-check
                        if (!_safetyManager.CanDisableService(sug.Name, sug.DisplayName))
                        {
                            result.Errors.Add($"{sug.Name}: SafetyManager servisi engelledi.");
                            break;
                        }

                        var oldStartType = _serviceManager.GetCurrentStartType(sug.Name);

                        if (_serviceManager.StopService(sug.Name, out var svcErr))
                        {
                            result.ServicesStopped++;
                            result.StoppedServiceNames.Add(sug.Name);

                            if (oldStartType.Equals("Auto", StringComparison.OrdinalIgnoreCase) ||
                                oldStartType.Equals("Automatic", StringComparison.OrdinalIgnoreCase))
                            {
                                _serviceManager.SetServiceStartType(sug.Name, ServiceStartMode.Manual, out _);
                            }

                            snapshot.AppliedChanges.Add(new AppliedChange
                            {
                                ChangeType = "ServiceStopped",
                                TargetName = sug.Name,
                                OldValue = "Running",
                                NewValue = "Stopped"
                            });
                        }
                        else if (!string.IsNullOrEmpty(svcErr))
                        {
                            result.Errors.Add($"{sug.Name}: {svcErr}");
                        }
                        break;

                    case SuggestionType.SetServiceManual:
                        progress?.Report(new OptimizationProgress { Message = $"Servis tipi Manuel yapılıyor: {sug.Name}", Percent = pct });

                        if (_safetyManager.CanDisableService(sug.Name, sug.DisplayName))
                        {
                            if (_serviceManager.SetServiceStartType(sug.Name, ServiceStartMode.Manual, out var manualErr))
                            {
                                result.ServicesSetToManual++;
                                snapshot.AppliedChanges.Add(new AppliedChange
                                {
                                    ChangeType = "ServiceStartTypeChanged",
                                    TargetName = sug.Name,
                                    OldValue = "Automatic",
                                    NewValue = "Manual"
                                });
                            }
                            else if (!string.IsNullOrEmpty(manualErr))
                            {
                                result.Errors.Add($"{sug.Name}: {manualErr}");
                            }
                        }
                        break;

                    case SuggestionType.DisableStartup:
                        progress?.Report(new OptimizationProgress { Message = $"Başlangıç devre dışı: {sug.Name}", Percent = pct });

                        if (!_safetyManager.CanDisableStartup(sug.Name))
                        {
                            result.Errors.Add($"{sug.Name}: Korumalı başlangıç öğesi.");
                            break;
                        }

                        bool isHKCU = sug.RegistryKey.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase);
                        var keyPath = sug.RegistryKey.Contains("\\")
                            ? sug.RegistryKey[(sug.RegistryKey.IndexOf('\\') + 1)..]
                            : sug.RegistryKey;

                        if (_startupManager.DisableStartupEntry(sug.Name, keyPath, isHKCU, out var startErr))
                        {
                            result.StartupItemsDisabled++;
                            result.DisabledStartupNames.Add(sug.Name);
                            snapshot.AppliedChanges.Add(new AppliedChange
                            {
                                ChangeType = "StartupDisabled",
                                TargetName = sug.Name,
                                OldValue = keyPath,
                                NewValue = "Disabled"
                            });
                        }
                        else if (!string.IsNullOrEmpty(startErr))
                        {
                            result.Errors.Add($"{sug.Name}: {startErr}");
                        }
                        break;

                    case SuggestionType.DisableScheduledTask:
                        progress?.Report(new OptimizationProgress { Message = $"Görev Zamanlayıcı devre dışı: {sug.DisplayName}", Percent = pct });

                        if (!_safetyManager.CanDisableScheduledTask(sug.Name))
                        {
                            result.Errors.Add($"{sug.Name}: Korumalı sistem görevi.");
                            break;
                        }

                        if (_taskManager.DisableTask(sug.Name, out var taskErr))
                        {
                            result.ScheduledTasksDisabled++;
                            result.DisabledTaskNames.Add(sug.DisplayName);
                            snapshot.AppliedChanges.Add(new AppliedChange
                            {
                                ChangeType = "ScheduledTaskDisabled",
                                TargetName = sug.Name,
                                OldValue = "Enabled",
                                NewValue = "Disabled"
                            });
                        }
                        else if (!string.IsNullOrEmpty(taskErr))
                        {
                            result.Errors.Add($"{sug.Name}: {taskErr}");
                        }
                        break;
                }

                await Task.Delay(50, ct); // Prevent CPU spike
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                result.Errors.Add($"{sug.Name}: {ex.Message}");
                LogService.Error($"Optimization step exception for {sug.Name}", ex);
            }
        }

        // Save updated snapshot with all applied changes
        _restoreService.SaveSnapshot(snapshot, out _);

        // 3. Requirement 13 & 12: Wait 3 seconds post-cleanup, re-measure RAM & detect restarts
        progress?.Report(new OptimizationProgress { Message = "Sistem bekleniyor & yeniden ölçülüyor (3sn)...", Percent = 90 });

        var restarted = await _restartDetector.MonitorTerminatedProcessesAsync(result.TerminatedProcessNames, durationSeconds: 3, ct);
        result.RestartedProcessCount = restarted.Count;
        result.RestartedProcessNames = restarted.Select(r => $"{r.ProcessName} (Kaynak: {r.SuspectedOrigin})").ToList();

        var ramAfter = Services.MemoryService.GetRamInfo();
        result.RamAfterBytes = ramAfter.UsedBytes;
        result.UsagePercentAfter = ramAfter.UsagePercent;

        // Net real gain
        result.RamSavedBytes = Math.Max(0, result.RamBeforeBytes - result.RamAfterBytes);
        result.Success = result.Errors.Count == 0;

        LogService.LogOptimizationEnd(result.RamSavedBytes);
        progress?.Report(new OptimizationProgress { Message = "Optimizasyon ve yeniden analiz tamamlandı.", Percent = 100 });

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
        SuggestionType.StopService => "Servis (Durdur)",
        SuggestionType.SetServiceManual => "Servis (Manuel Yap)",
        SuggestionType.DisableStartup => "Başlangıç",
        SuggestionType.DisableScheduledTask => "Görev Zamanlayıcı",
        _ => "Bilinmiyor"
    };
}

public enum SuggestionType
{
    TerminateProcess,
    StopService,
    SetServiceManual,
    DisableStartup,
    DisableScheduledTask
}
