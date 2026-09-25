namespace WinRamOptimizer.Models;

public enum StartupItemType
{
    Registry,
    StartupFolder,
    TaskScheduler
}

public class StartupItemModel
{
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public StartupItemType StartupType { get; set; }
    public string RegistryKey { get; set; } = string.Empty;
    public string ImpactLevel { get; set; } = "LOW"; // LOW, MEDIUM, HIGH
    public bool IsEnabled { get; set; } = true;
    public bool CanDisable { get; set; } = true;
    public RiskLevel Risk { get; set; } = RiskLevel.Low;
    public string Description { get; set; } = string.Empty;
    public int OptimizationScore { get; set; }

    public string StartupTypeDisplay => StartupType switch
    {
        StartupItemType.Registry => "Registry",
        StartupItemType.StartupFolder => "Başlangıç Klasörü",
        StartupItemType.TaskScheduler => "Görev Zamanlayıcı",
        _ => "Bilinmiyor"
    };

    public string ImpactDisplay => ImpactLevel switch
    {
        "HIGH" => "🔴 Yüksek",
        "MEDIUM" => "🟡 Orta",
        "LOW" => "🟢 Düşük",
        _ => "❓ Bilinmiyor"
    };
}
