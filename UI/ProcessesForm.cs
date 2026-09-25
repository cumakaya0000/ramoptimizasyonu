using WinRamOptimizer.Core;
using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.UI;

public class ProcessesForm : Form
{
    private static readonly Color BgDark = Color.FromArgb(18, 18, 18);
    private static readonly Color AccentBlue = Color.FromArgb(0, 120, 212);
    private static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
    private static readonly Color AccentOrange = Color.FromArgb(245, 158, 11);
    private static readonly Color AccentRed = Color.FromArgb(239, 68, 68);
    private static readonly Color TextPrimary = Color.FromArgb(240, 240, 240);
    private static readonly Color TextSecondary = Color.FromArgb(160, 160, 160);

    private readonly SafetyManager _safetyManager;
    private readonly ProcessManager _processManager;
    private readonly ProcessAnalyzer _processAnalyzer;
    private List<ProcessInfoModel> _allProcesses;
    private DataGridView _dgv = null!;
    private TextBox _txtSearch = null!;
    private Label _lblCount = null!;
    private CancellationTokenSource? _cts;

    public ProcessesForm(SafetyManager safetyManager, List<ProcessInfoModel>? existingData = null)
    {
        _safetyManager = safetyManager;
        _processManager = new ProcessManager(safetyManager);
        _processAnalyzer = new ProcessAnalyzer(safetyManager);
        _allProcesses = existingData ?? new List<ProcessInfoModel>();

        InitializeUI();
        if (_allProcesses.Any())
            PopulateGrid(_allProcesses);
        else
            _ = LoadProcessesAsync();
    }

    private void InitializeUI()
    {
        Text = "🔧 Process Yöneticisi";
        Size = new Size(1180, 680);
        MinimumSize = new Size(950, 500);
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterParent;

        // Toolbar
        var pnlToolbar = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(1180, 44),
            BackColor = Color.FromArgb(22, 22, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _txtSearch = new TextBox
        {
            Location = new Point(12, 9),
            Size = new Size(220, 26),
            BackColor = Color.FromArgb(42, 42, 42),
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "🔍 Process ara..."
        };
        _txtSearch.TextChanged += (_, _) => FilterGrid(_txtSearch.Text);

        var btnAll = MiniButton("☑ Tümünü Seç", Color.FromArgb(40, 40, 40), 240, 105);
        btnAll.Click += (_, _) => SetCheckAll(true);

        var btnSafe = MiniButton("☑ Güvenli Seç", Color.FromArgb(40, 40, 40), 350, 105);
        btnSafe.Click += (_, _) => SetCheckSafe();

        var btnNone = MiniButton("☐ Seçimi Kaldır", Color.FromArgb(40, 40, 40), 460, 110);
        btnNone.Click += (_, _) => SetCheckAll(false);

        var btnRefresh = MiniButton("🔄 Yenile", AccentBlue, 575, 80);
        btnRefresh.Click += async (_, _) => await LoadProcessesAsync();

        var btnTerminateSelected = MiniButton("❌ SEÇİLENLERİ KAPAT", Color.FromArgb(120, 40, 40), 660, 160);
        btnTerminateSelected.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnTerminateSelected.Click += BtnTerminateSelected_Click;

        _lblCount = new Label
        {
            Text = "–",
            Font = new Font("Segoe UI", 8f),
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(830, 14)
        };

        pnlToolbar.Controls.AddRange(new Control[] { _txtSearch, btnAll, btnSafe, btnNone, btnRefresh, btnTerminateSelected, _lblCount });
        Controls.Add(pnlToolbar);

        // DataGridView
        _dgv = new DataGridView
        {
            Location = new Point(0, 46),
            Size = new Size(1180, 634),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackgroundColor = BgDark,
            GridColor = Color.FromArgb(40, 40, 40),
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            ReadOnly = false, // Allow checkbox editing
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 32,
            RowTemplate = { Height = 28 }
        };

        StyleDgv(_dgv);
        AddColumns();
        _dgv.SortCompare += Dgv_SortCompare;

        Controls.Add(_dgv);
    }

    private void AddColumns()
    {
        // Checkbox column
        var chkCol = new DataGridViewCheckBoxColumn
        {
            Name = "Select",
            HeaderText = "Seç",
            FillWeight = 40,
            ReadOnly = false
        };
        _dgv.Columns.Add(chkCol);

        var cols = new (string Name, string Header, int Width, DataGridViewContentAlignment Align)[]
        {
            ("Process",      "Process Adı",  160, DataGridViewContentAlignment.MiddleLeft),
            ("PID",          "PID",           60, DataGridViewContentAlignment.MiddleCenter),
            ("RAM",          "RAM",          100, DataGridViewContentAlignment.MiddleRight),
            ("CPU",          "CPU %",         70, DataGridViewContentAlignment.MiddleRight),
            ("Publisher",    "Yayıncı",      180, DataGridViewContentAlignment.MiddleLeft),
            ("Path",         "Dosya Yolu",   220, DataGridViewContentAlignment.MiddleLeft),
            ("Category",     "Kategori",     110, DataGridViewContentAlignment.MiddleCenter),
            ("Risk",         "Risk",          80, DataGridViewContentAlignment.MiddleCenter),
            ("Score",        "Puan",          60, DataGridViewContentAlignment.MiddleCenter),
        };

        foreach (var (name, header, width, align) in cols)
        {
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                FillWeight = width,
                ReadOnly = true,
                DefaultCellStyle = { Alignment = align, BackColor = BgDark, ForeColor = TextPrimary }
            });
        }
    }

    private static Button MiniButton(string text, Color back, int x, int w)
    {
        var b = new Button
        {
            Text = text,
            Location = new Point(x, 8),
            Size = new Size(w, 28),
            BackColor = back,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8f)
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private async Task LoadProcessesAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        _dgv.Rows.Clear();
        _lblCount.Text = "Taranıyor...";

        try
        {
            _allProcesses = await _processAnalyzer.GetAllProcessesAsync(null, _cts.Token);
            InvokeIfRequired(() => PopulateGrid(_allProcesses));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            LogService.Error("ProcessesForm load failed", ex);
        }
    }

    private void PopulateGrid(List<ProcessInfoModel> processes)
    {
        _dgv.Rows.Clear();

        foreach (var p in processes)
        {
            int rowIdx = _dgv.Rows.Add(
                false, // Select checkbox
                p.Name,
                p.Pid,
                p.RamUsageDisplay,
                $"{p.CpuUsagePercent:F1}%",
                p.Publisher,
                p.FilePath,
                p.CategoryDisplay,
                p.RiskDisplay,
                p.OptimizationScore
            );

            var row = _dgv.Rows[rowIdx];
            row.Tag = p;

            // Disable checkbox for protected/critical/active window processes
            if (!p.CanTerminate || p.IsSystemProcess || p.IsActiveWindow)
            {
                row.Cells["Select"].ReadOnly = true;
                row.Cells["Select"].Value = false;
            }

            // Color by category
            Color rowColor = p.Category switch
            {
                ProcessCategory.Green => Color.FromArgb(22, 28, 22),
                ProcessCategory.Yellow => Color.FromArgb(30, 26, 18),
                ProcessCategory.Red => Color.FromArgb(32, 20, 20),
                _ => BgDark
            };
            row.DefaultCellStyle.BackColor = rowColor;

            // Risk cell color
            row.Cells["Risk"].Style.ForeColor = p.Risk switch
            {
                RiskLevel.Low => AccentGreen,
                RiskLevel.Medium => AccentOrange,
                RiskLevel.High => AccentRed,
                RiskLevel.Critical => AccentRed,
                _ => TextSecondary
            };
        }

        _lblCount.Text = $"{processes.Count} process | Sonuç: {_dgv.RowCount}";
    }

    private void SetCheckAll(bool check)
    {
        foreach (DataGridViewRow row in _dgv.Rows)
        {
            if (row.Tag is ProcessInfoModel p && p.CanTerminate && !p.IsSystemProcess && !p.IsActiveWindow)
            {
                row.Cells["Select"].Value = check;
            }
        }
    }

    private void SetCheckSafe()
    {
        foreach (DataGridViewRow row in _dgv.Rows)
        {
            if (row.Tag is ProcessInfoModel p)
            {
                bool isSafe = p.CanTerminate && p.Risk == RiskLevel.Low && (p.Category == ProcessCategory.Red || p.AppCategory == ApplicationCategory.Updater);
                if (p.IsActiveWindow) isSafe = false;
                row.Cells["Select"].Value = isSafe;
            }
        }
    }

    private void FilterGrid(string filter)
    {
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allProcesses
            : _allProcesses.Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                       p.Publisher.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        PopulateGrid(filtered);
    }

    private void BtnTerminateSelected_Click(object? sender, EventArgs e)
    {
        var selected = new List<ProcessInfoModel>();
        foreach (DataGridViewRow row in _dgv.Rows)
        {
            if (Convert.ToBoolean(row.Cells["Select"].Value) && row.Tag is ProcessInfoModel p)
            {
                if (_safetyManager.CanTerminateProcess(p.Name, p.Pid))
                    selected.Add(p);
            }
        }

        if (!selected.Any())
        {
            MessageBox.Show("Sonlandırılacak process seçilmedi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Seçilen {selected.Count} adet process sonlandırılsın mı?\n" +
            $"Toplam Tahmini RAM Kazanımı: {selected.Sum(s => s.RamUsageMB):F0} MB",
            "Processleri Sonlandır", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        int count = 0;
        var errors = new List<string>();

        foreach (var proc in selected)
        {
            if (_processManager.TerminateProcess(proc.Pid, proc.Name, proc.RamUsageBytes, out var err))
            {
                count++;
            }
            else if (!string.IsNullOrEmpty(err))
            {
                errors.Add($"{proc.Name}: {err}");
            }
        }

        var msg = $"{count} adet process başarıyla sonlandırıldı.";
        if (errors.Any()) msg += $"\n\nBazı hatalar oluştu:\n{string.Join("\n", errors.Take(5))}";

        MessageBox.Show(msg, "İşlem Tamamlandı", MessageBoxButtons.OK, count > 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        _ = LoadProcessesAsync();
    }

    private void Dgv_SortCompare(object? sender, DataGridViewSortCompareEventArgs e)
    {
        if (e.Column.Name == "RAM")
        {
            static double ParseRam(string s)
            {
                if (string.IsNullOrEmpty(s)) return 0;
                s = s.Replace(" MB", "").Replace(" GB", "").Trim();
                return double.TryParse(s, out var v) ? v : 0;
            }
            e.SortResult = ParseRam(e.CellValue1?.ToString() ?? "")
                .CompareTo(ParseRam(e.CellValue2?.ToString() ?? ""));
            e.Handled = true;
        }
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
