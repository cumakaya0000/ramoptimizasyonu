using System.ServiceProcess;
using WinRamOptimizer.Models;

namespace WinRamOptimizer.Services;

public class WindowsServiceManager
{
    private readonly Core.SafetyManager _safetyManager;

    public WindowsServiceManager(Core.SafetyManager safetyManager)
    {
        _safetyManager = safetyManager;
    }

    public bool StopService(string serviceName, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!_safetyManager.CanDisableService(serviceName))
        {
            errorMessage = $"'{serviceName}' servisi korumalı listededir ve durdurulamaz.";
            LogService.Warning($"Blocked attempt to stop protected service: {serviceName}");
            return false;
        }

        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                errorMessage = "Servis zaten durdurulmuş.";
                return true;
            }

            var oldStatus = sc.Status.ToString();
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            LogService.LogServiceChanged(serviceName, oldStatus, "Stopped");
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            LogService.Error($"Failed to stop service: {serviceName}", ex);
            return false;
        }
    }

    public bool SetServiceStartType(string serviceName, ServiceStartMode newMode, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!_safetyManager.CanDisableService(serviceName))
        {
            errorMessage = $"'{serviceName}' servisi korumalıdır.";
            return false;
        }

        try
        {
            using var sc = new ServiceController(serviceName);
            var oldType = GetStartTypeString(sc);

            // Use SC command to change start type (ServiceController doesn't expose this directly)
            var result = RunScCommand($"config \"{serviceName}\" start= {GetScStartType(newMode)}");
            if (!result)
            {
                errorMessage = "sc.exe komutu başarısız oldu.";
                return false;
            }

            LogService.LogServiceChanged(serviceName, sc.Status.ToString(), sc.Status.ToString(), oldType, newMode.ToString());
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            LogService.Error($"Failed to set start type for: {serviceName}", ex);
            return false;
        }
    }

    public bool StartService(string serviceName, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Running)
            {
                return true;
            }
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            LogService.Info($"Service started: {serviceName}");
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            LogService.Error($"Failed to start service: {serviceName}", ex);
            return false;
        }
    }

    public string GetCurrentStartType(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return GetStartTypeString(sc);
        }
        catch { return "Unknown"; }
    }

    private static string GetStartTypeString(ServiceController sc)
    {
        try
        {
            // Use WMI to get start type
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT StartMode FROM Win32_Service WHERE Name='{sc.ServiceName}'");
            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                return obj["StartMode"]?.ToString() ?? "Unknown";
            }
        }
        catch { }
        return "Unknown";
    }

    private static bool RunScCommand(string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("sc.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(10000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    private static string GetScStartType(ServiceStartMode mode) => mode switch
    {
        ServiceStartMode.Automatic => "auto",
        ServiceStartMode.Manual => "demand",
        ServiceStartMode.Disabled => "disabled",
        _ => "demand"
    };
}
