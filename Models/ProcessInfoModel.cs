using WinRamOptimizer.Core;

namespace WinRamOptimizer.Models;

public enum ProcessCategory
{
    Green,   // Safe / Required
    Yellow,  // Optional
    Red      // Likely unnecessary background app
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public class ProcessInfoModel
{
    public string Name { get; set; } = string.Empty;
    public int Pid { get; set; }
    public long RamUsageBytes { get; set; }
    public double CpuUsagePercent { get; set; }
    public DateTime? StartTime { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public bool IsSignedByMicrosoft { get; set; }
    public bool IsSystemProcess { get; set; }
    public bool IsUserApplication { get; set; }
    public bool IsActiveWindow { get; set; } // Currently active foreground user application
    public ApplicationCategory AppCategory { get; set; } = ApplicationCategory.Unknown;
    public ProcessCategory Category { get; set; }
    public RiskLevel Risk { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public int OptimizationScore { get; set; }
    public bool CanTerminate { get; set; }

    public double RamUsageMB => RamUsageBytes / (1024.0 * 1024.0);
    public double RamUsageGB => RamUsageBytes / (1024.0 * 1024.0 * 1024.0);

    public string RamUsageDisplay
    {
        get
        {
            if (RamUsageMB >= 1024)
                return $"{RamUsageGB:F1} GB";
            return $"{RamUsageMB:F0} MB";
        }
    }

    public string CategoryDisplay => Category switch
    {
        ProcessCategory.Green => "✅ Güvenli",
        ProcessCategory.Yellow => "⚠️ Opsiyonel",
        ProcessCategory.Red => "🔴 Gereksiz",
        _ => "Bilinmiyor"
    };

    public string RiskDisplay => Risk switch
    {
        RiskLevel.Low => "LOW",
        RiskLevel.Medium => "MEDIUM",
        RiskLevel.High => "HIGH",
        RiskLevel.Critical => "CRITICAL",
        _ => "UNKNOWN"
    };

    public string AppCategoryDisplay => ApplicationClassifier.GetCategoryDisplayName(AppCategory);
}
