using System.Diagnostics;
using System.IO;
using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.Core;

public class ScheduledTaskAnalyzer
{
    private readonly SafetyManager _safetyManager;

    public ScheduledTaskAnalyzer(SafetyManager safetyManager)
    {
        _safetyManager = safetyManager;
    }

    public async Task<List<ScheduledTaskInfoModel>> GetAllTasksAsync(CancellationToken ct = default)
    {
        return await Task.Run(() => GetAllTasks(ct), ct);
    }

    public List<ScheduledTaskInfoModel> GetAllTasks(CancellationToken ct = default)
    {
        var tasks = new List<ScheduledTaskInfoModel>();

        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", "/query /fo CSV /v")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var p = Process.Start(psi);
            if (p == null) return tasks;

            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(15000);

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length <= 1) return tasks; // Only header or empty

            var headers = ParseCsvRow(lines[0]);
            int nameIdx = FindColumnIndex(headers, "TaskName", "Görev Adı", "Task Name");
            int statusIdx = FindColumnIndex(headers, "Status", "Durum");
            int runAsIdx = FindColumnIndex(headers, "Task To Run", "Çalıştırılacak Görev", "Author", "Yazar");

            for (int i = 1; i < lines.Length; i++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var cols = ParseCsvRow(lines[i]);
                    if (cols.Length <= nameIdx || nameIdx < 0) continue;

                    var taskPath = cols[nameIdx].Trim('"', ' ');
                    if (string.IsNullOrWhiteSpace(taskPath)) continue;

                    var status = statusIdx >= 0 && cols.Length > statusIdx ? cols[statusIdx].Trim('"', ' ') : "Ready";
                    var cmd = runAsIdx >= 0 && cols.Length > runAsIdx ? cols[runAsIdx].Trim('"', ' ') : string.Empty;

                    var model = BuildTaskModel(taskPath, status, cmd);
                    if (model != null)
                        tasks.Add(model);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            LogService.Error("ScheduledTaskAnalyzer scan failed", ex);
        }

        return tasks.OrderBy(t => t.TaskName).ToList();
    }

    private ScheduledTaskInfoModel? BuildTaskModel(string taskPath, string status, string command)
    {
        // Ignore Microsoft core Windows tasks
        if (taskPath.StartsWith("\\Microsoft\\Windows\\", StringComparison.OrdinalIgnoreCase) ||
            taskPath.StartsWith("\\Microsoft\\Windows Defender\\", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var isMicrosoft = taskPath.StartsWith("\\Microsoft\\", StringComparison.OrdinalIgnoreCase);
        var taskName = Path.GetFileName(taskPath.TrimEnd('\\'));
        if (string.IsNullOrEmpty(taskName)) taskName = taskPath;

        var isEnabled = !status.Equals("Disabled", StringComparison.OrdinalIgnoreCase) &&
                        !status.Equals("Devre Dışı", StringComparison.OrdinalIgnoreCase);

        var model = new ScheduledTaskInfoModel
        {
            TaskName = taskName,
            TaskPath = taskPath,
            ExecuteCommand = command,
            IsEnabled = isEnabled,
            IsMicrosoftTask = isMicrosoft,
            CanDisable = _safetyManager.CanDisableScheduledTask(taskPath)
        };

        // Categorize & Score
        ClassifyTask(model);

        return model;
    }

    private static void ClassifyTask(ScheduledTaskInfoModel model)
    {
        var nameLower = model.TaskName.ToLowerInvariant();
        var pathLower = model.TaskPath.ToLowerInvariant();
        var cmdLower = model.ExecuteCommand.ToLowerInvariant();

        if (model.IsMicrosoftTask)
        {
            model.Category = ScheduledTaskCategory.SystemTask;
            model.Risk = RiskLevel.High;
            model.Description = "Microsoft sistem görevi.";
            model.OptimizationScore = 0;
            return;
        }

        if (nameLower.Contains("update") || nameLower.Contains("updater") || cmdLower.Contains("update") || cmdLower.Contains("autoupdate"))
        {
            model.Category = ScheduledTaskCategory.Updater;
            model.Risk = RiskLevel.Low;
            model.Description = "Üçüncü parti arka plan güncelleme görevi.";
            model.OptimizationScore = 80;
            return;
        }

        if (nameLower.Contains("telemetry") || nameLower.Contains("report") || nameLower.Contains("analytics"))
        {
            model.Category = ScheduledTaskCategory.Telemetry;
            model.Risk = RiskLevel.Low;
            model.Description = "Telemetri / Veri toplama görevi.";
            model.OptimizationScore = 90;
            return;
        }

        if (nameLower.Contains("launch") || nameLower.Contains("start") || nameLower.Contains("helper"))
        {
            model.Category = ScheduledTaskCategory.AutoLaunch;
            model.Risk = RiskLevel.Low;
            model.Description = "Otomatik başlatıcı / Yardımcı bileşen görevi.";
            model.OptimizationScore = 70;
            return;
        }

        model.Category = ScheduledTaskCategory.Unknown;
        model.Risk = RiskLevel.Medium;
        model.Description = "Üçüncü parti scheduled task.";
        model.OptimizationScore = 40;
    }

    private static int FindColumnIndex(string[] headers, params string[] candidates)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var h = headers[i].Trim('"', ' ');
            foreach (var c in candidates)
            {
                if (h.Equals(c, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }
        return -1;
    }

    private static string[] ParseCsvRow(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                current.Append(c);
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}
