using WinRamOptimizer.Core;
using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.UI;

public class StartupForm : Form
{
    private static readonly Color BgDark = Color.FromArgb(18, 18, 18);
    private static readonly Color AccentBlue = Color.FromArgb(0, 120, 212);
    private static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
    private static readonly Color AccentOrange = Color.FromArgb(245, 158, 11);
    private static readonly Color AccentRed = Color.FromArgb(239, 68, 68);
    private static readonly Color TextPrimary = Color.FromArgb(240, 240, 240);
    private static readonly Color TextSecondary = Color.FromArgb(160, 160, 160);

    private readonly SafetyManager _safetyManager;
    private readonly StartupManager _startupManager;
    private readonly StartupAnalyzer _startupAnalyzer;
    private List<StartupItemModel> _allItems;
    private DataGridView _dgv = null!;
    private CancellationTokenSource? _cts;

    public StartupForm(SafetyManager safetyManager, List<StartupItemModel>? existingData = null)
    {
        _safetyManager = safetyManager;
        _startupManager = new StartupManager(safetyManager);
        _startupAnalyzer = new StartupAnalyzer(safetyManager);
        _allItems = existingData ?? new List<StartupItemModel>();

        InitializeUI();
        if (_allItems.Any())
            PopulateGrid(_allItems);
        else
            _ = LoadItemsAsync();
    }

    private void InitializeUI()
    {
        Text = "🚀 Başlangıç Programları";
        Size = new Size(1060, 600);
        MinimumSize = new Size(800, 480);
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterParent;

        var pnlToolbar = new Panel
        {
            Location = new Point(0, 0), Size = new Size(1060, 44),
            BackColor = Color.FromArgb(22, 22, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var btnRefresh = MiniBtn("🔄 Yenile", AccentBlue, 12);
        btnRefresh.Click += async (_, _) => await LoadItemsAsync();

        var btnDisable = MiniBtn("🚫 Başlangıçtan Kaldır", Color.FromArgb(100, 40, 40), 118);
        btnDisable.Width = 170;
        btnDisable.Click += BtnDisable_Click;

        var lblInfo = new Label
        {
            Text = "Programları başlangıçtan kaldırmak, Windows'un açılışını hızlandırır.",
            Font = new Font("Segoe UI", 8f),
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(304, 14)
        };

        pnlToolbar.Controls.AddRange(new Control[] { btnRefresh, btnDisable, lblInfo });
        Controls.Add(pnlToolbar);

        _dgv = new DataGridView
        {
            Location = new Point(0, 46), Size = new Size(1060, 554),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, BackgroundColor = BgDark,
            GridColor = Color.FromArgb(40, 40, 40), BorderStyle = BorderStyle.None,
            RowHeadersVisible = false, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 32, RowTemplate = { Height = 28 }
        };

        StyleDgv(_dgv);

        var cols = new (string Name, string Header, int Weight)[]
        {
            ("Name",        "Program Adı",    160),
            ("Publisher",   "Publisher",       160),
            ("Type",        "Başlangıç Türü",  110),
            ("Impact",      "Etki",             80),
            ("Risk",        "Risk",             80),
            ("Score",       "Puan",             60),
            ("Status",      "Durum",            80),
            ("Path",        "Dosya Yolu",      270),
        };

        foreach (var (name, header, weight) in cols)
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = header, FillWeight = weight });

        Controls.Add(_dgv);
    }

    private static Button MiniBtn(string text, Color back, int x)
    {
        var b = new Button { Text = text, Location = new Point(x, 8), Size = new Size(100, 28), BackColor = back, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private async Task LoadItemsAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _dgv.Rows.Clear();

        try
        {
            _allItems = await _startupAnalyzer.GetAllStartupItemsAsync(_cts.Token);
            InvokeIfRequired(() => PopulateGrid(_allItems));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { LogService.Error("StartupForm load failed", ex); }
    }

    private void PopulateGrid(List<StartupItemModel> items)
    {
        _dgv.Rows.Clear();
        foreach (var item in items)
        {
            int idx = _dgv.Rows.Add(
                item.Name,
                item.Publisher,
                item.StartupTypeDisplay,
                item.ImpactDisplay,
                item.Risk.ToString(),
                item.OptimizationScore,
                item.IsEnabled ? "Etkin" : "Devre Dışı",
                item.FilePath
            );

            var row = _dgv.Rows[idx];
            row.Tag = item;

            var impactColor = item.ImpactLevel switch
            {
                "HIGH" => AccentRed,
                "MEDIUM" => AccentOrange,
                _ => AccentGreen
            };
            row.Cells["Impact"].Style.ForeColor = impactColor;
            row.Cells["Status"].Style.ForeColor = item.IsEnabled ? AccentGreen : TextSecondary;
        }
    }

    private void BtnDisable_Click(object? sender, EventArgs e)
    {
        if (_dgv.SelectedRows.Count == 0) return;
        if (_dgv.SelectedRows[0].Tag is not StartupItemModel item) return;

        if (!item.CanDisable)
        {
            MessageBox.Show($"'{item.Name}' korumalıdır.", "Korumalı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"'{item.Name}' başlangıçtan kaldırılsın mı?\n\nBu, Windows açılışını hızlandırabilir.",
            "Başlangıçtan Kaldır", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        bool ok = false;
        string err = string.Empty;

        if (item.StartupType == StartupItemType.Registry)
        {
            bool isHKCU = item.RegistryKey.StartsWith("HKCU");
            var keyPath = item.RegistryKey.Contains("\\")
                ? item.RegistryKey[(item.RegistryKey.IndexOf('\\') + 1)..]
                : item.RegistryKey;
            ok = _startupManager.DisableStartupEntry(item.Name, keyPath, isHKCU, out err);
        }
        else if (item.StartupType == StartupItemType.StartupFolder)
        {
            ok = _startupManager.DisableStartupFolderEntry(item.FilePath, out err);
        }
        else
        {
            MessageBox.Show("Görev Zamanlayıcı öğeleri bu sürümde devre dışı bırakılamıyor.", "Bilgi",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (ok)
            MessageBox.Show("Başlangıçtan başarıyla kaldırıldı.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show($"İşlem başarısız:\n{err}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);

        _ = LoadItemsAsync();
    }

    private static void StyleDgv(DataGridView dgv)
    {
        dgv.DefaultCellStyle.BackColor = Color.FromArgb(24, 24, 24);
        dgv.DefaultCellStyle.ForeColor = Color.FromArgb(220, 220, 220);
        dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 80, 150);
        dgv.DefaultCellStyle.SelectionForeColor = Color.White;
        dgv.DefaultCellStyle.Font = new Font("Segoe UI", 8.5f);
        dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(20, 20, 20);
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(160, 160, 160);
        dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        dgv.EnableHeadersVisualStyles = false;
        dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(28, 28, 28);
    }

    private void InvokeIfRequired(Action action)
    {
        if (InvokeRequired) Invoke(action);
        else action();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        base.OnFormClosed(e);
    }
}
