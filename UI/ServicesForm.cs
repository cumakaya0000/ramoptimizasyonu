using WinRamOptimizer.Core;
using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.UI;

public class ServicesForm : Form
{
    private static readonly Color BgDark = Color.FromArgb(18, 18, 18);
    private static readonly Color BgCard = Color.FromArgb(30, 30, 30);
    private static readonly Color AccentBlue = Color.FromArgb(0, 120, 212);
    private static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
    private static readonly Color AccentOrange = Color.FromArgb(245, 158, 11);
    private static readonly Color AccentRed = Color.FromArgb(239, 68, 68);
    private static readonly Color TextPrimary = Color.FromArgb(240, 240, 240);
    private static readonly Color TextSecondary = Color.FromArgb(160, 160, 160);

    private readonly SafetyManager _safetyManager;
    private readonly WindowsServiceManager _serviceManager;
    private readonly ServiceAnalyzer _serviceAnalyzer;
    private List<ServiceInfoModel> _allServices;
    private DataGridView _dgv = null!;
    private TextBox _txtSearch = null!;
    private Label _lblCount = null!;
    private CancellationTokenSource? _cts;

    public ServicesForm(SafetyManager safetyManager, List<ServiceInfoModel>? existingData = null)
    {
        _safetyManager = safetyManager;
        _serviceManager = new WindowsServiceManager(safetyManager);
        _serviceAnalyzer = new ServiceAnalyzer(safetyManager);
        _allServices = existingData ?? new List<ServiceInfoModel>();

        InitializeUI();
        if (_allServices.Any())
            PopulateGrid(_allServices);
        else
            _ = LoadServicesAsync();
    }

    private void InitializeUI()
    {
        Text = "⚙️ Servis Yöneticisi";
        Size = new Size(1120, 680);
        MinimumSize = new Size(900, 500);
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterParent;

        // Toolbar
        var pnlToolbar = new Panel
        {
            Location = new Point(0, 0), Size = new Size(1120, 44),
            BackColor = Color.FromArgb(22, 22, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _txtSearch = new TextBox
        {
            Location = new Point(12, 9), Size = new Size(250, 26),
            BackColor = Color.FromArgb(42, 42, 42), ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "🔍 Servis ara..."
        };
        _txtSearch.TextChanged += (_, _) => FilterGrid(_txtSearch.Text);

        var btnRefresh = MiniButton("🔄 Yenile", AccentBlue, 276);
        btnRefresh.Click += async (_, _) => await LoadServicesAsync();

        var btnStop = MiniButton("⏹ Durdur", Color.FromArgb(100, 40, 40), 378);
        btnStop.Click += BtnStop_Click;

        var btnStart = MiniButton("▶ Başlat", Color.FromArgb(30, 80, 30), 488);
        btnStart.Click += BtnStart_Click;

        _lblCount = new Label { Text = "–", AutoSize = true, Location = new Point(600, 14), ForeColor = TextSecondary };

        pnlToolbar.Controls.AddRange(new Control[] { _txtSearch, btnRefresh, btnStop, btnStart, _lblCount });
        Controls.Add(pnlToolbar);

        // DGV
        _dgv = new DataGridView
        {
            Location = new Point(0, 46), Size = new Size(1120, 634),
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

        var cols = new (string Name, string Header, int Width)[]
        {
            ("ServiceName",   "Servis Adı",      140),
            ("DisplayName",   "Görünen Ad",       200),
            ("Status",        "Durum",             80),
            ("StartType",     "Başlangıç",        100),
            ("Category",      "Kategori",          90),
            ("RAM",           "RAM Tahmini",       90),
            ("Dependents",    "Bağımlılar",        80),
            ("Recommendation","Öneri",            260),
        };

        foreach (var (name, header, width) in cols)
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = header, FillWeight = width });

        Controls.Add(_dgv);
    }

    private static Button MiniButton(string text, Color back, int x)
    {
        var b = new Button
        {
            Text = text, Location = new Point(x, 8), Size = new Size(96, 28),
            BackColor = back, ForeColor = Color.White, FlatStyle = FlatStyle.Flat
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private async Task LoadServicesAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _dgv.Rows.Clear();
        _lblCount.Text = "Taranıyor...";

        try
        {
            _allServices = await _serviceAnalyzer.GetAllServicesAsync(null, _cts.Token);
            InvokeIfRequired(() => PopulateGrid(_allServices));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { LogService.Error("ServicesForm load failed", ex); }
    }

    private void PopulateGrid(List<ServiceInfoModel> services)
    {
        _dgv.Rows.Clear();
        foreach (var svc in services)
        {
            int idx = _dgv.Rows.Add(
                svc.ServiceName,
                svc.DisplayName,
                svc.Status,
                svc.StartType,
                svc.CategoryDisplay,
                svc.EstimatedRamDisplay,
                svc.DependentServices.Count > 0 ? svc.DependentServices.Count.ToString() : "–",
                svc.Recommendation
            );

            var row = _dgv.Rows[idx];
            row.Tag = svc;

            // Row color by category
            row.DefaultCellStyle.BackColor = svc.Category switch
            {
                ServiceCategory.Critical => Color.FromArgb(28, 20, 20),
                ServiceCategory.Safe => Color.FromArgb(20, 28, 20),
                ServiceCategory.Optional => Color.FromArgb(28, 26, 18),
                ServiceCategory.Unknown => Color.FromArgb(24, 24, 28),
                _ => BgDark
            };

            // Status color
            var statusCell = row.Cells["Status"];
            statusCell.Style.ForeColor = svc.Status == "Running" ? AccentGreen : TextSecondary;
        }

        _lblCount.Text = $"{services.Count} servis";
    }

    private void FilterGrid(string filter)
    {
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allServices
            : _allServices.Where(s =>
                s.ServiceName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                s.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        PopulateGrid(filtered);
    }

    private void BtnStop_Click(object? sender, EventArgs e)
    {
        if (_dgv.SelectedRows.Count == 0) return;
        if (_dgv.SelectedRows[0].Tag is not ServiceInfoModel svc) return;

        if (!svc.CanDisable)
        {
            MessageBox.Show($"'{svc.DisplayName}' korumalıdır ve durdurulamaz.", "Korumalı Servis",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (svc.DependentServices.Count > 0)
        {
            var deps = string.Join(", ", svc.DependentServices.Take(5));
            var cont = MessageBox.Show(
                $"Bu servise şu servisler bağımlıdır:\n{deps}\n\nYine de durdurmak istiyor musunuz?",
                "Bağımlılık Uyarısı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (cont != DialogResult.Yes) return;
        }

        var confirm = MessageBox.Show(
            $"'{svc.DisplayName}' servisi durdurulsun mu?",
            "Servisi Durdur", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        if (_serviceManager.StopService(svc.ServiceName, out var err))
        {
            MessageBox.Show("Servis başarıyla durduruldu.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _ = LoadServicesAsync();
        }
        else
            MessageBox.Show($"Durdurma başarısız:\n{err}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void BtnStart_Click(object? sender, EventArgs e)
    {
        if (_dgv.SelectedRows.Count == 0) return;
        if (_dgv.SelectedRows[0].Tag is not ServiceInfoModel svc) return;

        if (_serviceManager.StartService(svc.ServiceName, out var err))
        {
            MessageBox.Show("Servis başlatıldı.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _ = LoadServicesAsync();
        }
        else
            MessageBox.Show($"Başlatma başarısız:\n{err}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
