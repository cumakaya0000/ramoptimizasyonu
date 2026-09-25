using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.Core;

public class RamAnalyzer
{
    /// <summary>
    /// Gets current RAM information using native Windows API.
    /// </summary>
    public RamInfo GetCurrentRamInfo()
    {
        return MemoryService.GetRamInfo();
    }

    /// <summary>
    /// Calculates estimated optimizable RAM based on process analysis.
    /// </summary>
    public long CalculateOptimizableRam(IEnumerable<ProcessInfoModel> processes)
    {
        return processes
            .Where(p => p.Category == ProcessCategory.Red || p.Category == ProcessCategory.Yellow)
            .Where(p => p.CanTerminate)
            .Sum(p => p.RamUsageBytes);
    }

    /// <summary>
    /// Returns a human-readable RAM summary.
    /// </summary>
    public string GetRamSummary()
    {
        var info = GetCurrentRamInfo();
        return $"Toplam: {info.TotalGB:F1} GB | " +
               $"Kullanılan: {info.UsedGB:F1} GB | " +
               $"Boş: {info.FreeGB:F1} GB | " +
               $"%{info.UsagePercent:F0} Kullanım";
    }
}
