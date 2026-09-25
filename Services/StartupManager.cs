using Microsoft.Win32;

namespace WinRamOptimizer.Services;

public class StartupManager
{
    private static readonly string[] RegistryKeys = new[]
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
    };

    private readonly Core.SafetyManager _safetyManager;

    public StartupManager(Core.SafetyManager safetyManager)
    {
        _safetyManager = safetyManager;
    }

    /// <summary>
    /// Disables a startup entry by removing the registry value or moving to disabled key.
    /// </summary>
    public bool DisableStartupEntry(string name, string registryKeyPath, bool isHKCU, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!_safetyManager.CanDisableStartup(name))
        {
            errorMessage = $"'{name}' başlangıç öğesi korumalıdır.";
            return false;
        }

        try
        {
            var hive = isHKCU ? Registry.CurrentUser : Registry.LocalMachine;
            using var key = hive.OpenSubKey(registryKeyPath, writable: true);
            if (key == null)
            {
                errorMessage = "Registry anahtarı bulunamadı.";
                return false;
            }

            var value = key.GetValue(name);
            if (value == null)
            {
                errorMessage = "Registry değeri bulunamadı.";
                return false;
            }

            // Save backup in disabled key
            var disabledKeyPath = registryKeyPath + @"\AutorunsDisabled";
            using var disabledKey = hive.CreateSubKey(disabledKeyPath);
            disabledKey?.SetValue(name, value);

            // Remove from Run key
            key.DeleteValue(name);

            LogService.LogStartupDisabled(name, registryKeyPath);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            LogService.Error($"Failed to disable startup: {name}", ex);
            return false;
        }
    }

    /// <summary>
    /// Re-enables a previously disabled startup entry.
    /// </summary>
    public bool EnableStartupEntry(string name, string registryKeyPath, bool isHKCU, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            var hive = isHKCU ? Registry.CurrentUser : Registry.LocalMachine;
            var disabledKeyPath = registryKeyPath + @"\AutorunsDisabled";

            using var disabledKey = hive.OpenSubKey(disabledKeyPath, writable: true);
            if (disabledKey == null)
            {
                errorMessage = "Devre dışı bırakılmış öğe bulunamadı.";
                return false;
            }

            var value = disabledKey.GetValue(name);
            if (value == null)
            {
                errorMessage = "Backup değeri bulunamadı.";
                return false;
            }

            using var runKey = hive.OpenSubKey(registryKeyPath, writable: true);
            runKey?.SetValue(name, value);
            disabledKey.DeleteValue(name);

            LogService.Info($"Startup re-enabled: {name}");
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            LogService.Error($"Failed to enable startup: {name}", ex);
            return false;
        }
    }

    /// <summary>
    /// Disables a startup folder shortcut by renaming it.
    /// </summary>
    public bool DisableStartupFolderEntry(string filePath, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            if (!File.Exists(filePath))
            {
                errorMessage = "Dosya bulunamadı.";
                return false;
            }

            var disabledPath = filePath + ".disabled";
            File.Move(filePath, disabledPath);
            LogService.Info($"Startup folder entry disabled: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            LogService.Error($"Failed to disable startup folder entry: {filePath}", ex);
            return false;
        }
    }

    public bool EnableStartupFolderEntry(string disabledFilePath, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            if (!File.Exists(disabledFilePath))
            {
                errorMessage = "Devre dışı bırakılmış dosya bulunamadı.";
                return false;
            }

            var originalPath = disabledFilePath.Replace(".disabled", "");
            File.Move(disabledFilePath, originalPath);
            LogService.Info($"Startup folder entry enabled: {originalPath}");
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
