namespace WinRamOptimizer.Models;

public enum ServiceCategory
{
    Critical,
    Safe,
    Optional,
    Unknown
}

public class ServiceInfoModel
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StartType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long EstimatedRamBytes { get; set; }
    public ServiceCategory Category { get; set; }
    public RiskLevel Risk { get; set; }
    public bool CanDisable { get; set; }
    public bool CanSetManual { get; set; } = true;
    public bool IsProtected { get; set; }
    public bool IsHardwareService { get; set; }
    public List<string> DependentServices { get; set; } = new();
    public List<string> DependsOn { get; set; } = new();
    public string Recommendation { get; set; } = string.Empty;

    public double EstimatedRamMB => EstimatedRamBytes / (1024.0 * 1024.0);

    public string EstimatedRamDisplay
    {
        get
        {
            if (EstimatedRamMB < 1) return "< 1 MB";
            if (EstimatedRamMB >= 1024) return $"{EstimatedRamBytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
            return $"{EstimatedRamMB:F0} MB";
        }
    }

    public string CategoryDisplay => Category switch
    {
        ServiceCategory.Critical => "🔴 Kritik",
        ServiceCategory.Safe => "✅ Güvenli",
        ServiceCategory.Optional => "⚠️ Opsiyonel",
        ServiceCategory.Unknown => "❓ Bilinmiyor",
        _ => "Bilinmiyor"
    };
}
