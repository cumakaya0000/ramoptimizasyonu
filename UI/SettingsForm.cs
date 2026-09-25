using Newtonsoft.Json;
using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.UI;

public class SettingsForm : Form
{
    private static readonly Color BgDark = Color.FromArgb(18, 18, 18);
    private static readonly Color BgCard = Color.FromArgb(30, 30, 30);
    private static readonly Color AccentBlue = Color.FromArgb(0, 120, 212);
    private static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
    private static readonly Color TextPrimary = Color.FromArgb(240, 240, 240);
    private static readonly Color TextSecondary = Color.FromArgb(160, 160, 160);

    private static readonly string SettingsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Config", "settings.json");

    private AppSettings _settings = new();
    private ComboBox _cboProfile = null!;
    private CheckBox _chkWorkingSetTrim = null!;
    private CheckBox _chkAutoAnalyzeOnStart = null!;

    public SettingsForm()
    {
        LoadSettings();
        InitializeUI();
    }

    private void InitializeUI()
    {
        Text = "⚙ Ayarlar";
        Size = new Size(760, 620);
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var tabs = new TabControl
        {
            Location = new Point(8, 8),
            Size = new Size(738, 540),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        StyleTabControl(tabs);

        // ── General tab ────────────────────────────────────────────────────────
        var tabGeneral = new TabPage("⚙ Genel") { BackColor = BgCard, ForeColor = TextPrimary };
        BuildGeneralTab(tabGeneral);

        // ── Profile tab ────────────────────────────────────────────────────────
        var tabProfile = new TabPage("👤 Sistem Profili") { BackColor = BgCard, ForeColor = TextPrimary };
        BuildProfileTab(tabProfile);

        // ── Log tab ────────────────────────────────────────────────────────────
        var tabLog = new TabPage("📋 Log") { BackColor = BgCard, ForeColor = TextPrimary };
        BuildLogTab(tabLog);

        // ── About tab ──────────────────────────────────────────────────────────
        var tabAbout = new TabPage("ℹ Hakkında") { BackColor = BgCard, ForeColor = TextPrimary };
        BuildAboutTab(tabAbout);

        tabs.TabPages.AddRange(new[] { tabGeneral, tabProfile, tabLog, tabAbout });
        Controls.Add(tabs);

        var btnSave = new Button
        {
            Text = "💾  Kaydet",
            Location = new Point(614, 558),
            Size = new Size(130, 34),
            BackColor = AccentGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += (_, _) => { SaveSettings(); Close(); };

        var btnCancel = new Button
        {
            Text = "İptal",
            Location = new Point(536, 558),
            Size = new Size(72, 34),
            BackColor = Color.FromArgb(60, 40, 40),
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (_, _) => Close();

        Controls.AddRange(new Control[] { btnSave, btnCancel });
    }

    private void BuildGeneralTab(TabPage tab)
    {
        int y = 20;

        _chkAutoAnalyzeOnStart = AddCheck(tab, "Başlangıçta otomatik analiz yap", _settings.AutoAnalyzeOnStart, ref y);
        _ = AddCheck(tab, "Sistem tepsisinde çalıştır (Sistem Tepsisi)", false, ref y);

        AddSectionLabel(tab, "RAM Optimizasyon Yöntemi", ref y);

        _chkWorkingSetTrim = AddCheck(tab, "Working Set Trim uygula (Geçici RAM boşaltma – performansı olumsuz etkileyebilir)", _settings.EnableWorkingSetTrim, ref y);

        AddNote(tab, "⚠ Working Set Trim yöntemi RAM sayacını geçici olarak düşürür,\nancak disk swap'ını artırabilir. Yalnızca düşük RAM'li sistemlerde önerilir.", ref y);
    }

    private void BuildProfileTab(TabPage tab)
    {
        int y = 20;
        AddSectionLabel(tab, "Sistem Kullanım Profili", ref y);
        AddNote(tab, "Profil seçimi, hangi servis ve uygulamaların 'gerekli' sayılacağını etkiler.", ref y);

        _cboProfile = new ComboBox
        {
            Location = new Point(16, y),
            Size = new Size(280, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(42, 42, 42),
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat
        };
        _cboProfile.Items.AddRange(Enum.GetNames<SystemProfile>());
        _cboProfile.SelectedIndex = (int)_settings.SystemProfile;
        tab.Controls.Add(_cboProfile);
        y += 40;

        // Profile descriptions
        var descriptions = new Dictionary<string, string>
        {
            ["Gaming"] = "Steam, Xbox, GPU servisleri ve ses servisleri korunur.",
            ["Office"] = "Microsoft Office, tarayıcılar önceliklendirilir.",
            ["Developer"] = "Docker, WSL, SQL Server, Visual Studio, Node.js önemli kabul edilir.",
            ["LowRamPC"] = "Mümkün olan en fazla optimizasyon yapılır.",
            ["MaximumPerformance"] = "Gereksiz tüm arka plan öğeleri kapatılır.",
            ["Custom"] = "Tüm kurallar manuel olarak belirlenir."
        };

        foreach (var (profile, desc) in descriptions)
        {
            AddNote(tab, $"• {profile}: {desc}", ref y);
            y -= 12;
        }
    }

    private void BuildLogTab(TabPage tab)
    {
        var rtb = new RichTextBox
        {
            Location = new Point(8, 8),
            Size = new Size(716, 440),
            BackColor = Color.FromArgb(12, 12, 12),
            ForeColor = Color.FromArgb(160, 220, 160),
            Font = new Font("Consolas", 8f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        rtb.Text = LogService.ReadLogs(500);
        rtb.SelectionStart = rtb.TextLength;
        rtb.ScrollToCaret();

        var btnClear = new Button
        {
            Text = "🗑 Logu Temizle",
            Location = new Point(8, 458),
            Size = new Size(130, 28),
            BackColor = Color.FromArgb(80, 40, 40),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnClear.FlatAppearance.BorderSize = 0;
        btnClear.Click += (_, _) =>
        {
            try { File.WriteAllText(LogService.GetLogFilePath(), string.Empty); rtb.Clear(); }
            catch { }
        };

        var btnOpenFolder = new Button
        {
            Text = "📂 Log Klasörünü Aç",
            Location = new Point(148, 458),
            Size = new Size(160, 28),
            BackColor = Color.FromArgb(30, 60, 30),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnOpenFolder.FlatAppearance.BorderSize = 0;
        btnOpenFolder.Click += (_, _) =>
        {
            try { System.Diagnostics.Process.Start("explorer.exe", Path.GetDirectoryName(LogService.GetLogFilePath())!); }
            catch { }
        };

        tab.Controls.AddRange(new Control[] { rtb, btnClear, btnOpenFolder });
    }

    private static void BuildAboutTab(TabPage tab)
    {
        var lbl = new Label
        {
            Text =
                "WinRam Optimizer v1.0\n" +
                "─────────────────────────────────────\n\n" +
                "Platform: C# .NET 8 | Windows Forms\n" +
                "Geliştirici Hedefi: Windows 10/11\n\n" +
                "Bu uygulama sisteminize zarar verecek işlemler yapmaz.\n" +
                "Tüm değişiklikler önce analiz edilir, ardından kullanıcı\n" +
                "onayı alınır ve backup oluşturulur.\n\n" +
                "Güvenlik İlkesi:\n" +
                "• Sistem processleri hiçbir zaman kapatılmaz.\n" +
                "• Korumalı Windows servisleri devre dışı bırakılamaz.\n" +
                "• Tüm değişiklikler geri alınabilir.\n\n" +
                "Korunan Servisler: DHCP, DNS, RPC, Windows Defender,\n" +
                "Windows Firewall, Audio, Windows Update ve daha fazlası.\n\n" +
                "Log Konumu: [Uygulama Klasörü]/Logs/app.log\n" +
                "Backup Konumu: [Uygulama Klasörü]/Backups/",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(180, 180, 180),
            Location = new Point(16, 16),
            AutoSize = true
        };
        tab.Controls.Add(lbl);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static CheckBox AddCheck(TabPage tab, string text, bool @checked, ref int y)
    {
        var cb = new CheckBox
        {
            Text = text,
            Checked = @checked,
            Location = new Point(16, y),
            AutoSize = true,
            ForeColor = Color.FromArgb(210, 210, 210),
            BackColor = Color.Transparent
        };
        tab.Controls.Add(cb);
        y += 28;
        return cb;
    }

    private static void AddSectionLabel(TabPage tab, string text, ref int y)
    {
        tab.Controls.Add(new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 120, 212),
            Location = new Point(16, y),
            AutoSize = true
        });
        y += 24;
    }

    private static void AddNote(TabPage tab, string text, ref int y)
    {
        tab.Controls.Add(new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(130, 130, 130),
            Location = new Point(24, y),
            AutoSize = true
        });
        y += 36;
    }

    private static void StyleTabControl(TabControl tc)
    {
        tc.DrawItem += (_, e) =>
        {
            var g = e.Graphics;
            var tab = tc.TabPages[e.Index];
            var bounds = tc.GetTabRect(e.Index);

            bool selected = e.Index == tc.SelectedIndex;
            g.FillRectangle(new SolidBrush(selected ? Color.FromArgb(30, 30, 30) : Color.FromArgb(20, 20, 20)), bounds);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(tab.Text, new Font("Segoe UI", 8.5f, selected ? FontStyle.Bold : FontStyle.Regular),
                new SolidBrush(selected ? Color.White : Color.FromArgb(140, 140, 140)), bounds, sf);
        };
    }

    // ── Persistence ────────────────────────────────────────────────────────────

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { _settings = new AppSettings(); }
    }

    private void SaveSettings()
    {
        try
        {
            _settings.EnableWorkingSetTrim = _chkWorkingSetTrim.Checked;
            _settings.AutoAnalyzeOnStart = _chkAutoAnalyzeOnStart.Checked;
            _settings.SystemProfile = (SystemProfile)(_cboProfile?.SelectedIndex ?? 0);

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(_settings, Formatting.Indented));
            LogService.Info("Settings saved.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ayarlar kaydedilemedi: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

public class AppSettings
{
    public bool EnableWorkingSetTrim { get; set; } = false;
    public bool AutoAnalyzeOnStart { get; set; } = false;
    public SystemProfile SystemProfile { get; set; } = SystemProfile.Office;
}
