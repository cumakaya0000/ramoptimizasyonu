using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using WinRamOptimizer.Models;

namespace WinRamOptimizer.Core;

public enum ApplicationCategory
{
    System,
    Driver,
    Security,
    UserApplication,
    Browser,
    GameLauncher,
    Updater,
    CloudSync,
    Communication,
    OEMUtility,
    Development,
    Unknown
}

public static class ApplicationClassifier
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // Known user apps mapping
    private static readonly Dictionary<string, ApplicationCategory> KnownApps = new(StringComparer.OrdinalIgnoreCase)
    {
        // Browsers
        { "chrome", ApplicationCategory.Browser },
        { "msedge", ApplicationCategory.Browser },
        { "firefox", ApplicationCategory.Browser },
        { "opera", ApplicationCategory.Browser },
        { "brave", ApplicationCategory.Browser },
        { "iexplore", ApplicationCategory.Browser },
        { "vivaldi", ApplicationCategory.Browser },

        // Communication
        { "Discord", ApplicationCategory.Communication },
        { "Teams", ApplicationCategory.Communication },
        { "ms-teams", ApplicationCategory.Communication },
        { "Slack", ApplicationCategory.Communication },
        { "zoom", ApplicationCategory.Communication },
        { "skype", ApplicationCategory.Communication },
        { "Telegram", ApplicationCategory.Communication },
        { "WhatsApp", ApplicationCategory.Communication },
        { "Viber", ApplicationCategory.Communication },

        // Game Launchers
        { "Steam", ApplicationCategory.GameLauncher },
        { "steamwebhelper", ApplicationCategory.GameLauncher },
        { "EpicGamesLauncher", ApplicationCategory.GameLauncher },
        { "GOGGalaxy", ApplicationCategory.GameLauncher },
        { "Battle.net", ApplicationCategory.GameLauncher },
        { "EADesktop", ApplicationCategory.GameLauncher },
        { "Origin", ApplicationCategory.GameLauncher },
        { "Uplay", ApplicationCategory.GameLauncher },
        { "upc", ApplicationCategory.GameLauncher },

        // Cloud Sync
        { "OneDrive", ApplicationCategory.CloudSync },
        { "Dropbox", ApplicationCategory.CloudSync },
        { "GoogleDriveFS", ApplicationCategory.CloudSync },
        { "iCloudServices", ApplicationCategory.CloudSync },

        // Updaters
        { "AdobeUpdateService", ApplicationCategory.Updater },
        { "Creative Cloud", ApplicationCategory.Updater },
        { "GoogleUpdate", ApplicationCategory.Updater },
        { "MicrosoftEdgeUpdate", ApplicationCategory.Updater },

        // Development
        { "devenv", ApplicationCategory.Development },
        { "Code", ApplicationCategory.Development },
        { "rider64", ApplicationCategory.Development },
        { "idea64", ApplicationCategory.Development },
        { "pycharm64", ApplicationCategory.Development },
        { "node", ApplicationCategory.Development },
        { "docker", ApplicationCategory.Development },

        // Security
        { "MsMpEng", ApplicationCategory.Security },
        { "NisSrv", ApplicationCategory.Security },
        { "SecurityHealthService", ApplicationCategory.Security },
        { "avp", ApplicationCategory.Security },
        { "mbam", ApplicationCategory.Security }
    };

    // Hardware vendors for Driver/OEM identification
    private static readonly HashSet<string> HardwareVendors = new(StringComparer.OrdinalIgnoreCase)
    {
        "Intel", "AMD", "Advanced Micro Devices", "NVIDIA", "NVIDIA Corporation",
        "Realtek", "Realtek Semiconductor", "Lenovo", "Synaptics", "ELAN", "ELAN Microelectronics",
        "Qualcomm", "MediaTek", "Asus", "ASUSTeK", "Dell", "HP", "Hewlett-Packard", "MSI", "Gigabyte", "Logitech"
    };

    /// <summary>
    /// Gets the PID of the process that currently owns the foreground active window.
    /// </summary>
    public static int GetActiveWindowProcessId()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return -1;
            GetWindowThreadProcessId(hwnd, out uint pid);
            return (int)pid;
        }
        catch { return -1; }
    }

    /// <summary>
    /// Classifies an application based on process name, file path, and publisher.
    /// </summary>
    public static ApplicationCategory Classify(string processName, string filePath = "", string publisher = "")
    {
        if (string.IsNullOrWhiteSpace(processName)) return ApplicationCategory.Unknown;

        var name = Path.GetFileNameWithoutExtension(processName);

        // Direct known app match
        if (KnownApps.TryGetValue(name, out var cat))
            return cat;

        // Updaters heuristic
        if (name.Contains("update", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("updater", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("autoupdate", StringComparison.OrdinalIgnoreCase))
        {
            return ApplicationCategory.Updater;
        }

        // OEM / Driver check by publisher
        if (!string.IsNullOrEmpty(publisher))
        {
            foreach (var vendor in HardwareVendors)
            {
                if (publisher.Contains(vendor, StringComparison.OrdinalIgnoreCase))
                {
                    if (name.Contains("update", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("telemetry", StringComparison.OrdinalIgnoreCase))
                        return ApplicationCategory.Updater;

                    return ApplicationCategory.OEMUtility;
                }
            }
        }

        // Generic classification
        if (name.StartsWith("sys", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("win", StringComparison.OrdinalIgnoreCase))
            return ApplicationCategory.System;

        return ApplicationCategory.UserApplication;
    }

    /// <summary>
    /// Returns a human-friendly Turkish category display name.
    /// </summary>
    public static string GetCategoryDisplayName(ApplicationCategory category) => category switch
    {
        ApplicationCategory.System => "⚙️ Sistem",
        ApplicationCategory.Driver => "🔌 Sürücü (Driver)",
        ApplicationCategory.Security => "🛡️ Güvenlik",
        ApplicationCategory.UserApplication => "💻 Kullanıcı Uygulaması",
        ApplicationCategory.Browser => "🌐 Tarayıcı",
        ApplicationCategory.GameLauncher => "🎮 Oyun Başlatıcı",
        ApplicationCategory.Updater => "🔄 Güncelleyici",
        ApplicationCategory.CloudSync => "☁️ Bulut Senkronizasyon",
        ApplicationCategory.Communication => "💬 İletişim Uygulaması",
        ApplicationCategory.OEMUtility => "🔧 Donanım / OEM Yazılımı",
        ApplicationCategory.Development => "🛠️ Geliştirici Aracı",
        _ => "❓ Bilinmiyor"
    };
}
