using System.Runtime.InteropServices;

namespace WinRamOptimizer.Services;

public static class MemoryService
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    public static Models.RamInfo GetRamInfo()
    {
        var status = new MEMORYSTATUSEX();
        status.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        GlobalMemoryStatusEx(ref status);

        long total = (long)status.ullTotalPhys;
        long free = (long)status.ullAvailPhys;
        long used = total - free;

        return new Models.RamInfo
        {
            TotalBytes = total,
            FreeBytes = free,
            UsedBytes = used,
            UsagePercent = (double)used / total * 100.0
        };
    }

    /// <summary>
    /// Trims the working set of a specific process.
    /// WARNING: This is a cosmetic operation that may decrease performance.
    /// </summary>
    public static bool TrimProcessWorkingSet(int pid)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);
            return EmptyWorkingSet(process.Handle);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Trims working sets of all non-system processes.
    /// WARNING: This is a temporary cosmetic operation.
    /// </summary>
    public static int TrimAllWorkingSets(Core.SafetyManager safetyManager)
    {
        int trimmed = 0;
        try
        {
            foreach (var process in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    if (safetyManager.CanTerminateProcess(process.ProcessName))
                    {
                        EmptyWorkingSet(process.Handle);
                        trimmed++;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            LogService.Error("TrimAllWorkingSets failed", ex);
        }
        return trimmed;
    }
}
