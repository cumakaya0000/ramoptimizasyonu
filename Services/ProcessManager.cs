using WinRamOptimizer.Models;

namespace WinRamOptimizer.Services;

public class ProcessManager
{
    private readonly Core.SafetyManager _safetyManager;

    public ProcessManager(Core.SafetyManager safetyManager)
    {
        _safetyManager = safetyManager;
    }

    /// <summary>
    /// Terminates a process by PID after safety checks.
    /// </summary>
    public bool TerminateProcess(int pid, string processName, long estimatedRamBytes, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!_safetyManager.CanTerminateProcess(processName))
        {
            errorMessage = $"'{processName}' sistem için kritik bir process olduğundan sonlandırılamaz.";
            LogService.Warning($"Blocked attempt to terminate protected process: {processName} (PID: {pid})");
            return false;
        }

        try
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);

            // Double-check: don't kill system processes
            if (process.SessionId == 0 && IsKnownSystemProcess(processName))
            {
                errorMessage = $"'{processName}' sistem oturumunda çalışan kritik bir process.";
                return false;
            }

            long ramBefore = 0;
            try { ramBefore = process.WorkingSet64; } catch { }

            process.Kill(entireProcessTree: false);
            process.WaitForExit(5000);

            LogService.LogProcessTerminated(processName, estimatedRamBytes > 0 ? estimatedRamBytes : ramBefore);
            return true;
        }
        catch (ArgumentException)
        {
            // Process already exited
            errorMessage = "Process zaten sonlanmış.";
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            LogService.Error($"Failed to terminate process: {processName} (PID: {pid})", ex);
            return false;
        }
    }

    private static bool IsKnownSystemProcess(string name)
    {
        var systemProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "system", "registry", "smss", "csrss", "wininit", "services",
            "lsass", "svchost", "winlogon", "dwm", "explorer", "audiodg",
            "fontdrvhost", "memory compression", "secure system"
        };
        return systemProcs.Contains(name);
    }
}
