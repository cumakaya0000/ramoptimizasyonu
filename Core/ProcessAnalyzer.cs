using System.Diagnostics;
using System.Management;
using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.Core;

public class ProcessAnalyzer
{
    private readonly SafetyManager _safetyManager;

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

        // Active foreground window PID
        int activePid = ApplicationClassifier.GetActiveWindowProcessId();

        var wmiCpuData = GetWmiCpuData();

        foreach (var proc in allProcesses)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var model = BuildProcessModel(proc, wmiCpuData, activePid);
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

    private ProcessInfoModel BuildProcessModel(Process proc, Dictionary<int, double> wmiCpuData, int activePid)
    {
        var model = new ProcessInfoModel
        {
            Name = proc.ProcessName,
            Pid = proc.Id,
            IsActiveWindow = (proc.Id == activePid && activePid > 0)
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

        // Application category
        model.AppCategory = ApplicationClassifier.Classify(proc.ProcessName, model.FilePath, model.Publisher);

        // Classify
        model.IsSystemProcess = IsSystemProcess(proc, model);
        model.IsUserApplication = model.AppCategory == ApplicationCategory.UserApplication ||
                                  model.AppCategory == ApplicationCategory.Browser ||
                                  model.AppCategory == ApplicationCategory.GameLauncher ||
                                  model.AppCategory == ApplicationCategory.Communication ||
                                  model.AppCategory == ApplicationCategory.CloudSync;

        model.CanTerminate = _safetyManager.CanTerminateProcess(proc.ProcessName, proc.Id);

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
        if (model.AppCategory == ApplicationCategory.System ||
            model.AppCategory == ApplicationCategory.Driver ||
            model.AppCategory == ApplicationCategory.Security)
            return true;

        if (proc.SessionId == 0) return true;
        return false;
    }

    private void ClassifyProcess(ProcessInfoModel model)
    {
        if (model.IsSystemProcess || !model.CanTerminate || model.IsActiveWindow)
        {
            model.Category = ProcessCategory.Green;
            model.Risk = RiskLevel.Critical;
            model.Recommendation = model.IsActiveWindow ? "⭐️ Aktif Kullanılan Uygulama." : "Sistem processi – dokunmayın.";
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
            if (model.RamUsageMB > 200)
            {
                model.Category = ProcessCategory.Red;
                model.Risk = RiskLevel.Low;
                model.Recommendation = "Arka planda yüksek RAM tüketiyor. Kapatabilirsiniz.";
            }
            else
            {
                model.Category = ProcessCategory.Yellow;
                model.Risk = RiskLevel.Low;
                model.Recommendation = "Arka plan uygulaması.";
            }
            return;
        }

        if (model.AppCategory == ApplicationCategory.Updater)
        {
            model.Category = ProcessCategory.Red;
            model.Risk = RiskLevel.Low;
            model.Recommendation = "Gereksiz arka plan güncelleyicisi.";
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
        if (model.IsSystemProcess || !model.CanTerminate || model.IsActiveWindow) return 0;
        if (model.IsSignedByMicrosoft) return 5;

        int score = 0;

        // RAM contribution (0-50 pts)
        score += (int)Math.Min(50, model.RamUsageMB / 10);

        // CPU contribution (0-20 pts)
        score += (int)Math.Min(20, model.CpuUsagePercent * 2);

        // Updater boost
        if (model.AppCategory == ApplicationCategory.Updater) score += 30;

        // User app penalty (if user might want it)
        if (model.IsUserApplication) score += 10;

        return Math.Max(0, Math.Min(100, score));
    }
}
