using System.ServiceProcess;
using WinRamOptimizer.Core;
using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.UI;

public class ServicesForm : Form
{
    private static readonly Color BgDark = Color.FromArgb(18, 18, 18);
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
        Size = new Size(1220, 680);
        MinimumSize = new Size(950, 500);
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterParent;

        // Toolbar
        var pnlToolbar = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(1220, 44),
            BackColor = Color.FromArgb(22, 22, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _txtSearch = new TextBox
        {
            Location = new Point(12, 9),
            Size = new Size(200, 26),
            BackColor = Color.FromArgb(42, 42, 42),
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "🔍 Servis ara..."
        };
        _txtSearch.TextChanged += (_, _) => FilterGrid(_txtSearch.Text);

        var btnSafe = MiniBtn("☑ Güvenlileri Seç", Color.FromArgb(40, 40, 40), 220, 115);
        btnSafe.Click += (_, _) => SetCheckPredicate(s => s.Category == ServiceCategory.Safe);

        var btnOptional = MiniBtn("☑ Opsiyonelleri Seç", Color.FromArgb(40, 40, 40), 340, 125);
        btnOptional.Click += (_, _) => SetCheckPredicate(s => s.Category == ServiceCategory.Optional);

        var btnAllEligible = MiniBtn("☑ Tüm Uygunları Seç", Color.FromArgb(40, 40, 40), 470, 125);
        btnAllEligible.Click += (_, _) => SetCheckPredicate(s => s.CanDisable && !s.IsProtected && !s.IsHardwareService);

        var btnNone = MiniBtn("☐ Seçimi Kaldır", Color.FromArgb(40, 40, 40), 600, 105);
        btnNone.Click += (_, _) => SetCheckPredicate(s => false);

        var btnStopSelected = MiniBtn("⏹ SEÇİLENLERİ DURDUR", Color.FromArgb(120, 40, 40), 710, 160);
        btnStopSelected.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnStopSelected.Click += BtnStopSelected_Click;

        var btnManualSelected = MiniBtn("⚙️ MANUEL YAP", Color.FromArgb(30, 80, 120), 875, 130);
        btnManualSelected.Click += BtnManualSelected_Click;

        var btnRefresh = MiniBtn("🔄", AccentBlue, 1010, 40);
        btnRefresh.Click += async (_, _) => await LoadServicesAsync();

        _lblCount = new Label { Text = "–", AutoSize = true, Location = new Point(1060, 14), ForeColor = TextSecondary };

        pnlToolbar.Controls.AddRange(new Control[] { _txtSearch, btnSafe, btnOptional, btnAllEligible, btnNone, btnStopSelected, btnManualSelected, btnRefresh, _lblCount });
        Controls.Add(pnlToolbar);

        // DGV
        _dgv = new DataGridView
        {
            Location = new Point(0, 46),
            Size = new Size(1220, 634),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackgroundColor = BgDark,
            GridColor = Color.FromArgb(40, 40, 40),
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 32,
            RowTemplate = { Height = 28 }
        };

        StyleDgv(_dgv);

        _dgv.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Select", HeaderText = "Seç", FillWeight = 35, ReadOnly = false });

        var cols = new (string Name, string Header, int Width)[]
        {
            ("ServiceName",   "Servis Adı",      140),
            ("DisplayName",   "Görünen Ad",       200),
            ("Status",        "Durum",             80),
            ("StartType",     "Başlangıç",        100),
            ("Category",      "Kategori",          90),
            ("RAM",           "RAM Tahmini",       90),
            ("Recommendation","Öneri",            260),
        };

        foreach (var (name, header, width) in cols)
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = header, FillWeight = width, ReadOnly = true });

        Controls.Add(_dgv);
    }

    private static Button MiniBtn(string text, Color back, int x, int w)
    {
        var b = new Button { Text = text, Location = new Point(x, 8), Size = new Size(w, 28), BackColor = back, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8f) };
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
                false,
                svc.ServiceName,
                svc.DisplayName,
                svc.Status,
                svc.StartType,
                svc.CategoryDisplay,
                svc.EstimatedRamDisplay,
                svc.Recommendation
            );

            var row = _dgv.Rows[idx];
            row.Tag = svc;

            // Requirement 6: Critical or Protected service checkboxes are disabled
            if (!svc.CanDisable || svc.IsProtected || svc.IsHardwareService || svc.Category == ServiceCategory.Critical)
            {
                row.Cells["Select"].ReadOnly = true;
                row.Cells["Select"].Value = false;
            }

            row.DefaultCellStyle.BackColor = svc.Category switch
            {
                ServiceCategory.Critical => Color.FromArgb(28, 20, 20),
                ServiceCategory.Safe => Color.FromArgb(20, 28, 20),
                ServiceCategory.Optional => Color.FromArgb(28, 26, 18),
                ServiceCategory.Unknown => Color.FromArgb(24, 24, 28),
                _ => BgDark
            };

            var statusCell = row.Cells["Status"];
            statusCell.Style.ForeColor = svc.Status == "Running" ? AccentGreen : TextSecondary;
        }

        _lblCount.Text = $"{services.Count} servis";
    }

    private void SetCheckPredicate(Func<ServiceInfoModel, bool> predicate)
    {
        foreach (DataGridViewRow row in _dgv.Rows)
        {
            if (row.Tag is ServiceInfoModel s && s.CanDisable && !s.IsProtected && !s.IsHardwareService)
            {
                row.Cells["Select"].Value = predicate(s);
            }
        }
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

    private void BtnStopSelected_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedServices();
        if (!selected.Any())
        {
            MessageBox.Show("Durdurulacak servis seçilmedi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show($"Seçilen {selected.Count} adet servis durdurulsun mu?", "Servisleri Durdur",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        int count = 0;
        var errors = new List<string>();

        foreach (var svc in selected)
        {
            if (_safetyManager.CanDisableService(svc.ServiceName, svc.DisplayName))
            {
                if (_serviceManager.StopService(svc.ServiceName, out var err))
                {
                    count++;
                }
                else if (!string.IsNullOrEmpty(err))
                {
                    errors.Add($"{svc.DisplayName}: {err}");
                }
            }
        }

        var msg = $"{count} adet servis başarıyla durduruldu.";
        if (errors.Any()) msg += $"\n\nHatalar:\n{string.Join("\n", errors.Take(5))}";

        MessageBox.Show(msg, "İşlem Tamamlandı", MessageBoxButtons.OK, count > 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        _ = LoadServicesAsync();
    }

    private void BtnManualSelected_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedServices();
        if (!selected.Any())
        {
            MessageBox.Show("İşlem yapılacak servis seçilmedi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show($"Seçilen {selected.Count} adet servisin başlangıç türü 'Manuel' yapılsın mı?", "Manuel Tipe Al",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        int count = 0;

        foreach (var svc in selected)
        {
            if (_safetyManager.CanDisableService(svc.ServiceName, svc.DisplayName))
            {
                if (_serviceManager.SetServiceStartType(svc.ServiceName, ServiceStartMode.Manual, out _))
                {
                    count++;
                }
            }
        }

        MessageBox.Show($"{count} adet servis Manuel başlangıç türüne alındı.", "İşlem Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);

        _ = LoadServicesAsync();
    }

    private List<ServiceInfoModel> GetSelectedServices()
    {
        var list = new List<ServiceInfoModel>();
        foreach (DataGridViewRow row in _dgv.Rows)
        {
            if (Convert.ToBoolean(row.Cells["Select"].Value) && row.Tag is ServiceInfoModel s)
            {
                if (_safetyManager.CanDisableService(s.ServiceName, s.DisplayName))
                    list.Add(s);
            }
        }
        return list;
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
