using System.IO;

namespace WinRamOptimizer.Services;

public static class LogService
{
    private static readonly object _lock = new();
    private static readonly string _logDirectory;
    private static readonly string _logFilePath;

    static LogService()
    {
        _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Directory.CreateDirectory(_logDirectory);
        _logFilePath = Path.Combine(_logDirectory, "app.log");
    }

    public static void Log(string message, string level = "INFO")
    {
        var entry = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] [{level}] {message}";
        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFilePath, entry + Environment.NewLine);
            }
            catch { /* Logging should never crash the app */ }
        }

        // Also output to debug console
        System.Diagnostics.Debug.WriteLine(entry);
    }

    public static void Info(string message) => Log(message, "INFO");
    public static void Warning(string message) => Log(message, "WARN");
    public static void Error(string message) => Log(message, "ERROR");
    public static void Error(string message, Exception ex) => Log($"{message} | Exception: {ex.Message}", "ERROR");

    public static void LogProcessTerminated(string processName, long ramSavedBytes)
    {
        var ramMB = ramSavedBytes / (1024.0 * 1024.0);
        Log($"Process terminated: {processName} | Yaklaşık RAM kazanımı: {ramMB:F0} MB", "ACTION");
    }

    public static void LogServiceChanged(string serviceName, string oldStatus, string newStatus, string oldStartType = "", string newStartType = "")
    {
        var msg = $"Service changed: {serviceName} | Durum: {oldStatus} -> {newStatus}";
        if (!string.IsNullOrEmpty(oldStartType))
            msg += $" | Başlangıç Tipi: {oldStartType} -> {newStartType}";
        Log(msg, "ACTION");
    }

    public static void LogStartupDisabled(string name, string registryKey)
    {
        Log($"Startup disabled: {name} | Key: {registryKey}", "ACTION");
    }

    public static void LogOptimizationStart()
    {
        Log("=== OPTİMİZASYON BAŞLADI ===", "ACTION");
    }

    public static void LogOptimizationEnd(long ramSavedBytes)
    {
        var ramMB = ramSavedBytes / (1024.0 * 1024.0);
        Log($"=== OPTİMİZASYON TAMAMLANDI | Toplam kazanım: {ramMB:F0} MB ===", "ACTION");
    }

    public static string GetLogFilePath() => _logFilePath;

    public static string ReadLogs(int lastLines = 200)
    {
        try
        {
            if (!File.Exists(_logFilePath)) return string.Empty;
            var lines = File.ReadAllLines(_logFilePath);
            var take = Math.Min(lastLines, lines.Length);
            return string.Join(Environment.NewLine, lines[^take..]);
        }
        catch { return string.Empty; }
    }
}
