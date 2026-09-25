using WinRamOptimizer.Core;
using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.UI;

public class ProcessesForm : Form
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
        Size = new Size(1100, 680);
        MinimumSize = new Size(900, 500);
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterParent;

        // Toolbar
        var pnlToolbar = new Panel { Location = new Point(0, 0), Size = new Size(1100, 44), BackColor = Color.FromArgb(22, 22, 22), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        _txtSearch = new TextBox
        {
            Location = new Point(12, 9),
            Size = new Size(250, 26),
            BackColor = Color.FromArgb(42, 42, 42),
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "🔍 Process ara..."
        };
        _txtSearch.TextChanged += (_, _) => FilterGrid(_txtSearch.Text);

        var btnRefresh = new Button
        {
            Text = "🔄 Yenile",
            Location = new Point(276, 8),
            Size = new Size(90, 28),
            BackColor = AccentBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnRefresh.FlatAppearance.BorderSize = 0;
        btnRefresh.Click += async (_, _) => await LoadProcessesAsync();

        var btnTerminate = new Button
        {
            Text = "❌ Sonlandır",
            Location = new Point(378, 8),
            Size = new Size(100, 28),
            BackColor = Color.FromArgb(100, 40, 40),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnTerminate.FlatAppearance.BorderSize = 0;
        btnTerminate.Click += BtnTerminate_Click;

        _lblCount = new Label
        {
            Text = "–",
            Font = new Font("Segoe UI", 8f),
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(490, 14)
        };

        pnlToolbar.Controls.AddRange(new Control[] { _txtSearch, btnRefresh, btnTerminate, _lblCount });
        Controls.Add(pnlToolbar);

        // DataGridView
        _dgv = new DataGridView
        {
            Location = new Point(0, 46),
            Size = new Size(1100, 634),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackgroundColor = BgDark,
            GridColor = Color.FromArgb(40, 40, 40),
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            ReadOnly = true,
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
        var cols = new (string Name, string Header, int Width, DataGridViewContentAlignment Align)[]
        {
            ("Process",     "Process",    180, DataGridViewContentAlignment.MiddleLeft),
            ("PID",         "PID",         60, DataGridViewContentAlignment.MiddleCenter),
            ("RAM",         "RAM",        100, DataGridViewContentAlignment.MiddleRight),
            ("CPU",         "CPU %",       70, DataGridViewContentAlignment.MiddleRight),
            ("Publisher",   "Publisher",  200, DataGridViewContentAlignment.MiddleLeft),
            ("Risk",        "Risk",        80, DataGridViewContentAlignment.MiddleCenter),
            ("Category",    "Kategori",   100, DataGridViewContentAlignment.MiddleCenter),
            ("Score",       "Puan",        60, DataGridViewContentAlignment.MiddleCenter),
            ("Recommendation","Öneri",    200, DataGridViewContentAlignment.MiddleLeft),
        };

        foreach (var (name, header, width, align) in cols)
        {
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                FillWeight = width,
                DefaultCellStyle = { Alignment = align, BackColor = BgDark, ForeColor = TextPrimary }
            });
        }
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
                p.Name,
                p.Pid,
                p.RamUsageDisplay,
                $"{p.CpuUsagePercent:F1}%",
                p.Publisher,
                p.RiskDisplay,
                p.CategoryDisplay,
                p.OptimizationScore,
                p.Recommendation
            );

            var row = _dgv.Rows[rowIdx];
            row.Tag = p;

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
            var riskCell = row.Cells["Risk"];
            riskCell.Style.ForeColor = p.Risk switch
            {
                RiskLevel.Low => AccentGreen,
                RiskLevel.Medium => AccentOrange,
                RiskLevel.High => AccentRed,
                RiskLevel.Critical => AccentRed,
                _ => TextSecondary
            };

            // RAM cell bold
            row.Cells["RAM"].Style.ForeColor = p.RamUsageMB > 500 ? AccentOrange : TextPrimary;
        }

        _lblCount.Text = $"{processes.Count} process | Arama sonuçları: {_dgv.RowCount}";
    }

    private void FilterGrid(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            PopulateGrid(_allProcesses);
            return;
        }

        var filtered = _allProcesses
            .Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                     || p.Publisher.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        PopulateGrid(filtered);
    }

    private void BtnTerminate_Click(object? sender, EventArgs e)
    {
        if (_dgv.SelectedRows.Count == 0) return;

        var row = _dgv.SelectedRows[0];
        if (row.Tag is not ProcessInfoModel proc) return;

        if (!proc.CanTerminate)
        {
            MessageBox.Show($"'{proc.Name}' process korumalıdır ve sonlandırılamaz.", "Korumalı Process",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"'{proc.Name}' (PID: {proc.Pid}) sonlandırılsın mı?\nRAM: {proc.RamUsageDisplay}",
            "Process Sonlandır", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        if (_processManager.TerminateProcess(proc.Pid, proc.Name, proc.RamUsageBytes, out var err))
        {
            MessageBox.Show("Process başarıyla sonlandırıldı.", "Başarılı",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            _ = LoadProcessesAsync();
        }
        else
        {
            MessageBox.Show($"Sonlandırma başarısız:\n{err}", "Hata",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Dgv_SortCompare(object? sender, DataGridViewSortCompareEventArgs e)
    {
        if (e.Column.Name == "RAM")
        {
            // Parse MB values for proper numeric sort
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
