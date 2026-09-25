namespace WinRamOptimizer.Models;

public enum ScheduledTaskCategory
{
    Updater,
    Launcher,
    Telemetry,
    BackgroundUpdate,
    AutoLaunch,
    Helper,
    SystemTask,
    Unknown
}

public class ScheduledTaskInfoModel
{
    public string TaskName { get; set; } = string.Empty;
    public string TaskPath { get; set; } = string.Empty;
    public string ExecuteCommand { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsMicrosoftTask { get; set; }
    public bool CanDisable { get; set; } = true;
    public ScheduledTaskCategory Category { get; set; } = ScheduledTaskCategory.Unknown;
    public RiskLevel Risk { get; set; } = RiskLevel.Low;
    public int OptimizationScore { get; set; }
    public string Description { get; set; } = string.Empty;

    public string CategoryDisplay => Category switch
    {
        ScheduledTaskCategory.Updater => "🔄 Güncelleyici",
        ScheduledTaskCategory.Launcher => "🚀 Başlatıcı",
        ScheduledTaskCategory.Telemetry => "📊 Telemetri",
        ScheduledTaskCategory.BackgroundUpdate => "☁️ Arka Plan Güncelleme",
        ScheduledTaskCategory.AutoLaunch => "⚡ Otomatik Başlatma",
        ScheduledTaskCategory.Helper => "🛠️ Yardımcı Bileşen",
        ScheduledTaskCategory.SystemTask => "🛡️ Sistem Görevi",
        _ => "❓ Bilinmiyor"
    };

    public string RiskDisplay => Risk switch
    {
        RiskLevel.Low => "LOW",
        RiskLevel.Medium => "MEDIUM",
        RiskLevel.High => "HIGH",
        RiskLevel.Critical => "CRITICAL",
        _ => "UNKNOWN"
    };
}
