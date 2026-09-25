using System.Management;
using System.ServiceProcess;
using Newtonsoft.Json;
using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.Core;

public class ServiceAnalyzer
{
    private readonly SafetyManager _safetyManager;
    private readonly List<SafeServiceEntry> _safeServices;

    public ServiceAnalyzer(SafetyManager safetyManager)
    {
        _safetyManager = safetyManager;
        _safeServices = LoadSafeServices();
    }

    private static List<SafeServiceEntry> LoadSafeServices()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config/safe-services.json");
            if (!File.Exists(path)) return new();
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<List<SafeServiceEntry>>(json) ?? new();
        }
        catch { return new(); }
    }

    public async Task<List<ServiceInfoModel>> GetAllServicesAsync(
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() => GetAllServices(progress, ct), ct);
    }

    public List<ServiceInfoModel> GetAllServices(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var result = new List<ServiceInfoModel>();
        var allServices = ServiceController.GetServices();
        int total = allServices.Length;
        int done = 0;

        // Get WMI details in bulk for performance
        var wmiDetails = GetWmiServiceDetails();

        foreach (var sc in allServices)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var model = BuildServiceModel(sc, wmiDetails);
                result.Add(model);
            }
            catch { }
            finally
            {
                done++;
                progress?.Report((int)((double)done / total * 100));
                try { sc.Dispose(); } catch { }
            }
        }

        return result.OrderBy(s => s.DisplayName).ToList();
    }

    private ServiceInfoModel BuildServiceModel(ServiceController sc, Dictionary<string, WmiServiceInfo> wmiDetails)
    {
        var model = new ServiceInfoModel
        {
            ServiceName = sc.ServiceName,
            DisplayName = sc.DisplayName,
            Status = sc.Status.ToString(),
            IsProtected = _safetyManager.IsProtectedService(sc.ServiceName),
            CanDisable = _safetyManager.CanDisableService(sc.ServiceName, sc.DisplayName),
            CanSetManual = _safetyManager.CanDisableService(sc.ServiceName, sc.DisplayName)
        };

        // WMI details
        if (wmiDetails.TryGetValue(sc.ServiceName, out var wmi))
        {
            model.Description = wmi.Description;
            model.StartType = wmi.StartMode;
            model.EstimatedRamBytes = wmi.ProcessId > 0 ? GetProcessRam(wmi.ProcessId) : 0;
            model.IsHardwareService = !_safetyManager.CanDisableService(sc.ServiceName, sc.DisplayName, wmi.PathName);
        }
        else
        {
            try { model.StartType = sc.StartType.ToString(); } catch { }
        }

        // Dependencies
        try
        {
            model.DependsOn = sc.ServicesDependedOn.Select(s => s.ServiceName).ToList();
            model.DependentServices = sc.DependentServices.Select(s => s.ServiceName).ToList();
        }
        catch { }

        // Category & risk
        ClassifyService(model);

        // Recommendation
        BuildRecommendation(model);

        return model;
    }

    private void ClassifyService(ServiceInfoModel model)
    {
        if (model.IsProtected || model.IsHardwareService)
        {
            model.Category = ServiceCategory.Critical;
            model.Risk = RiskLevel.Critical;
            return;
        }

        // Check against safe list
        var safeEntry = _safeServices.FirstOrDefault(
            s => s.ServiceName.Equals(model.ServiceName, StringComparison.OrdinalIgnoreCase));

        if (safeEntry != null)
        {
            model.Category = safeEntry.Category switch
            {
                "SAFE" => ServiceCategory.Safe,
                "OPTIONAL" => ServiceCategory.Optional,
                _ => ServiceCategory.Unknown
            };
            model.Risk = safeEntry.RiskLevel switch
            {
                "LOW" => RiskLevel.Low,
                "MEDIUM" => RiskLevel.Medium,
                "HIGH" => RiskLevel.High,
                _ => RiskLevel.Low
            };
            model.Description = string.IsNullOrEmpty(model.Description) ? safeEntry.Description : model.Description;
            return;
        }

        // Unknown
        model.Category = ServiceCategory.Unknown;
        model.Risk = RiskLevel.Medium;
    }

    private static void BuildRecommendation(ServiceInfoModel model)
    {
        if (model.Category == ServiceCategory.Critical || model.IsHardwareService)
        {
            model.Recommendation = "⛔ Kritik / Donanım servisi – dokunmayın.";
        }
        else if (model.Category == ServiceCategory.Safe && model.Status == "Running")
        {
            model.Recommendation = "✅ Bu servisi durdurabilirsiniz.";
        }
        else if (model.Category == ServiceCategory.Optional)
        {
            model.Recommendation = "⚠️ Kullanmıyorsanız devre dışı bırakabilir veya Manual yapabilirsiniz.";
        }
        else if (model.Category == ServiceCategory.Unknown)
        {
            model.Recommendation = "❓ Bilinmeyen servis – değiştirmeden önce araştırın.";
        }
        else
        {
            model.Recommendation = "Durum: " + model.Status;
        }
    }

    private static Dictionary<string, WmiServiceInfo> GetWmiServiceDetails()
    {
        var result = new Dictionary<string, WmiServiceInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Description, StartMode, ProcessId, PathName FROM Win32_Service");
            foreach (ManagementObject obj in searcher.Get())
            {
                try
                {
                    var name = obj["Name"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(name)) continue;
                    result[name] = new WmiServiceInfo
                    {
                        Description = obj["Description"]?.ToString() ?? string.Empty,
                        StartMode = obj["StartMode"]?.ToString() ?? string.Empty,
                        ProcessId = Convert.ToInt32(obj["ProcessId"] ?? 0),
                        PathName = obj["PathName"]?.ToString() ?? string.Empty
                    };
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    private static long GetProcessRam(int pid)
    {
        try
        {
            var proc = System.Diagnostics.Process.GetProcessById(pid);
            return proc.WorkingSet64;
        }
        catch { return 0; }
    }

    private record WmiServiceInfo
    {
        public string Description { get; init; } = string.Empty;
        public string StartMode { get; init; } = string.Empty;
        public int ProcessId { get; init; }
        public string PathName { get; init; } = string.Empty;
    }
}

internal class SafeServiceEntry
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "LOW";
}
