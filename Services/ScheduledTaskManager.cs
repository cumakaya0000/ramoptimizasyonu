using System.Diagnostics;
using WinRamOptimizer.Models;

namespace WinRamOptimizer.Services;

public class ScheduledTaskManager
{
    private readonly Core.SafetyManager _safetyManager;

    public ScheduledTaskManager(Core.SafetyManager safetyManager)
    {
        _safetyManager = safetyManager;
    }

    /// <summary>
    /// Disables a scheduled task using schtasks.exe.
    /// </summary>
    public bool DisableTask(string taskPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!_safetyManager.CanDisableScheduledTask(taskPath))
        {
            errorMessage = $"'{taskPath}' görevi korumalıdır ve devre dışı bırakılamaz.";
            LogService.Warning($"Blocked attempt to disable protected scheduled task: {taskPath}");
            return false;
        }

        try
        {
            // Escape quote task path
            var args = $"/Change /TN \"{taskPath}\" /DISABLE";
            var psi = new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var p = Process.Start(psi);
            if (p == null)
            {
                errorMessage = "schtasks.exe başlatılamadı.";
                return false;
            }

            var errOutput = p.StandardError.ReadToEnd();
            p.WaitForExit(10000);

            if (p.ExitCode == 0)
            {
                LogService.Info($"Scheduled Task disabled: {taskPath}");
                return true;
            }
            else
            {
                errorMessage = !string.IsNullOrWhiteSpace(errOutput) ? errOutput : $"schtasks exit code: {p.ExitCode}";
                LogService.Error($"Failed to disable scheduled task '{taskPath}': {errorMessage}");
                return false;
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            LogService.Error($"Exception disabling scheduled task '{taskPath}'", ex);
            return false;
        }
    }

    /// <summary>
    /// Re-enables a scheduled task using schtasks.exe.
    /// </summary>
    public bool EnableTask(string taskPath, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            var args = $"/Change /TN \"{taskPath}\" /ENABLE";
            var psi = new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var p = Process.Start(psi);
            p?.WaitForExit(10000);
            if (p?.ExitCode == 0)
            {
                LogService.Info($"Scheduled Task re-enabled: {taskPath}");
                return true;
            }
            errorMessage = $"schtasks exit code: {p?.ExitCode}";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
