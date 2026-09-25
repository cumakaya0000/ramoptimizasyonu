using System.Diagnostics;
using WinRamOptimizer.Models;

namespace WinRamOptimizer.Services;

public class RestartDetectionResult
{
    public string ProcessName { get; set; } = string.Empty;
    public bool Restarted { get; set; }
    public int NewPid { get; set; }
    public string SuspectedOrigin { get; set; } = string.Empty;
}

public class ProcessRestartDetector
{
    /// <summary>
    /// Monitors a list of terminated processes for specified seconds (default 5s) to detect if any auto-restarted.
    /// </summary>
    public async Task<List<RestartDetectionResult>> MonitorTerminatedProcessesAsync(
        List<string> terminatedNames,
        int durationSeconds = 5,
        CancellationToken ct = default)
    {
        var results = new List<RestartDetectionResult>();
        if (!terminatedNames.Any()) return results;

        var targetSet = new HashSet<string>(terminatedNames, StringComparer.OrdinalIgnoreCase);

        // Wait for monitoring duration
        await Task.Delay(TimeSpan.FromSeconds(durationSeconds), ct);

        try
        {
            var currentProcesses = Process.GetProcesses();
            foreach (var proc in currentProcesses)
            {
                try
                {
                    if (targetSet.Contains(proc.ProcessName))
                    {
                        var origin = TraceOrigin(proc.ProcessName);
                        results.Add(new RestartDetectionResult
                        {
                            ProcessName = proc.ProcessName,
                            Restarted = true,
                            NewPid = proc.Id,
                            SuspectedOrigin = origin
                        });
                        LogService.Warning($"Process restart detected: {proc.ProcessName} (PID: {proc.Id}) | Kaynak: {origin}");
                    }
                }
                catch { }
                finally { proc.Dispose(); }
            }
        }
        catch (Exception ex)
        {
            LogService.Error("ProcessRestartDetector error", ex);
        }

        return results;
    }

    private static string TraceOrigin(string processName)
    {
        // 1. Check if there's an active Windows service with matching name
        try
        {
            var services = System.ServiceProcess.ServiceController.GetServices();
            var svc = services.FirstOrDefault(s =>
                s.ServiceName.Contains(processName, StringComparison.OrdinalIgnoreCase) ||
                s.DisplayName.Contains(processName, StringComparison.OrdinalIgnoreCase));
            if (svc != null) return $"Windows Servisi ({svc.DisplayName})";
        }
        catch { }

        // 2. Default heuristic origin
        if (processName.Contains("update", StringComparison.OrdinalIgnoreCase))
            return "Arka Plan Güncelleyici / Scheduled Task";

        return "Otomatik Başlatıcı / Parent Process";
    }
}
