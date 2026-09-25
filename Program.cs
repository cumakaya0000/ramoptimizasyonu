using System.Security.Principal;
using System.Diagnostics;

namespace WinRamOptimizer;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Check for administrator privileges
        if (!IsAdministrator())
        {
            var result = MessageBox.Show(
                "WinRam Optimizer yönetici ayrıcalıkları gerektiriyor.\n\nYönetici olarak yeniden başlatılsın mı?",
                "Yönetici Gerekli",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                RestartAsAdmin();
            }
            return;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
        {
            Services.LogService.Error("Unhandled thread exception", e.Exception);
            MessageBox.Show($"Beklenmeyen hata: {e.Exception.Message}", "Hata",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Services.LogService.Error("Unhandled domain exception", ex);
        };

        Application.Run(new UI.MainForm());
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void RestartAsAdmin()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(exePath)) return;

            var psi = new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Yönetici olarak başlatılamadı: {ex.Message}",
                "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}