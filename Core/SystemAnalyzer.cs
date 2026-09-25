using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.Core;

/// <summary>
/// Top-level orchestrator that runs all analyzers in one pass.
/// </summary>
public class SystemAnalyzer
{
    private readonly RamAnalyzer _ramAnalyzer;
    private readonly ProcessAnalyzer _processAnalyzer;
    private readonly ServiceAnalyzer _serviceAnalyzer;
    private readonly StartupAnalyzer _startupAnalyzer;
    private readonly ScheduledTaskAnalyzer _taskAnalyzer;

    public SystemAnalyzer(SafetyManager safetyManager)
    {
        _ramAnalyzer = new RamAnalyzer();
        _processAnalyzer = new ProcessAnalyzer(safetyManager);
        _serviceAnalyzer = new ServiceAnalyzer(safetyManager);
        _startupAnalyzer = new StartupAnalyzer(safetyManager);
        _taskAnalyzer = new ScheduledTaskAnalyzer(safetyManager);
    }

    public RamInfo GetRamInfo() => _ramAnalyzer.GetCurrentRamInfo();

    public async Task<SystemAnalysisResult> RunFullAnalysisAsync(
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new SystemAnalysisResult();

        // Step 1: RAM
        progress?.Report(new AnalysisProgress { Step = "RAM bilgisi alınıyor...", Percent = 5 });
        result.RamInfo = _ramAnalyzer.GetCurrentRamInfo();

        // Step 2: Processes
        progress?.Report(new AnalysisProgress { Step = "Processler taranıyor...", Percent = 15 });
        var procProgress = new Progress<int>(p =>
            progress?.Report(new AnalysisProgress { Step = "Processler taranıyor...", Percent = 15 + (int)(p * 0.30) }));
        result.Processes = await _processAnalyzer.GetAllProcessesAsync(procProgress, ct);

        // Step 3: Services
        progress?.Report(new AnalysisProgress { Step = "Windows servisleri taranıyor...", Percent = 45 });
        var svcProgress = new Progress<int>(p =>
            progress?.Report(new AnalysisProgress { Step = "Windows servisleri taranıyor...", Percent = 45 + (int)(p * 0.25) }));
        result.Services = await _serviceAnalyzer.GetAllServicesAsync(svcProgress, ct);

        // Step 4: Startup items
        progress?.Report(new AnalysisProgress { Step = "Başlangıç programları taranıyor...", Percent = 70 });
        result.StartupItems = await _startupAnalyzer.GetAllStartupItemsAsync(ct);

        // Step 5: Scheduled Tasks
        progress?.Report(new AnalysisProgress { Step = "Görev Zamanlayıcı (Scheduled Tasks) taranıyor...", Percent = 85 });
        result.ScheduledTasks = await _taskAnalyzer.GetAllTasksAsync(ct);

        // Step 6: Optimizable RAM estimate
        progress?.Report(new AnalysisProgress { Step = "Optimizasyon skoru hesaplanıyor...", Percent = 95 });
        result.EstimatedOptimizableBytes = _ramAnalyzer.CalculateOptimizableRam(result.Processes);

        result.AnalyzedAt = DateTime.Now;
        progress?.Report(new AnalysisProgress { Step = "Analiz tamamlandı.", Percent = 100 });
        LogService.Info("Full system analysis completed with Scheduled Tasks.");
        return result;
    }
}

public class SystemAnalysisResult
{
    public DateTime AnalyzedAt { get; set; }
    public RamInfo RamInfo { get; set; } = new();
    public List<ProcessInfoModel> Processes { get; set; } = new();
    public List<ServiceInfoModel> Services { get; set; } = new();
    public List<StartupItemModel> StartupItems { get; set; } = new();
    public List<ScheduledTaskInfoModel> ScheduledTasks { get; set; } = new();
    public long EstimatedOptimizableBytes { get; set; }

    public int TotalProcessCount => Processes.Count;
    public int BackgroundProcessCount => Processes.Count(p => !p.IsUserApplication && !p.IsSystemProcess);
    public int ServiceCount => Services.Count;
    public int RunningServiceCount => Services.Count(s => s.Status == "Running");
    public int StartupCount => StartupItems.Count(s => s.IsEnabled);
    public int ScheduledTaskCount => ScheduledTasks.Count(t => t.IsEnabled);
}

public class AnalysisProgress
{
    public string Step { get; set; } = string.Empty;
    public int Percent { get; set; }
}
