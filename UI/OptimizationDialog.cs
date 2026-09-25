using WinRamOptimizer.Core;
using WinRamOptimizer.Models;

namespace WinRamOptimizer.UI;

/// <summary>
/// Shows optimization suggestions with multi-select filter buttons and dynamic RAM counter.
/// </summary>
public class OptimizationDialog : Form
{
    private static readonly Color BgDark = Color.FromArgb(18, 18, 18);
    private static readonly Color BgCard = Color.FromArgb(30, 30, 30);
    private static readonly Color AccentBlue = Color.FromArgb(0, 120, 212);
    private static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
    private static readonly Color TextPrimary = Color.FromArgb(240, 240, 240);
    private static readonly Color TextSecondary = Color.FromArgb(160, 160, 160);

    private readonly List<OptimizationSuggestion> _suggestions;
    private readonly OptimizationEngine _engine;
    private readonly List<CheckBox> _checkboxes = new();

    private Label _lblCounter = null!;
    private Label _lblTotal = null!;

    public List<OptimizationSuggestion> SelectedSuggestions { get; private set; } = new();

    public OptimizationDialog(List<OptimizationSuggestion> suggestions, OptimizationEngine engine)
    {
        _suggestions = suggestions;
        _engine = engine;
        InitializeUI();
        UpdateCounter();
    }

    private void InitializeUI()
    {
        Text = "⚡ Optimizasyon Önerileri";
        Size = new Size(820, 680);
        MinimumSize = new Size(750, 550);
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterParent;

        // Title
        var lblTitle = new Label
        {
            Text = "Optimizasyon Önerileri",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = AccentBlue,
            AutoSize = true,
            Location = new Point(16, 12)
        };

        // Total RAM estimate label
        _lblTotal = new Label
        {
            Text = "Tahmini kazanım: –",
            Font = new Font("Segoe UI", 10f),
            ForeColor = AccentGreen,
            AutoSize = true,
            Location = new Point(16, 40)
        };

        // Selected counter label
        _lblCounter = new Label
        {
            Text = "Seçili: 0 / 0",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = AccentBlue,
            AutoSize = true,
            Location = new Point(400, 40)
        };

        // Filter toolbar buttons (Requirement 4)
        var pnlFilterToolbar = new Panel
        {
            Location = new Point(12, 68),
            Size = new Size(780, 36),
            BackColor = Color.FromArgb(24, 24, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var btnAll = FilterBtn("☑ Tümünü Seç", 4, 110);
        btnAll.Click += (_, _) => SetChecked(s => true);

        var btnNone = FilterBtn("☐ Seçimi Kaldır", 118, 110);
        btnNone.Click += (_, _) => SetChecked(s => false);

        var btnSafe = FilterBtn("☑ Güvenli Seç", 232, 105);
        btnSafe.Click += (_, _) => SetChecked(s => s.Risk == RiskLevel.Low);

        var btnProc = FilterBtn("☑ Processler", 341, 100);
        btnProc.Click += (_, _) => SetChecked(s => s.Type == SuggestionType.TerminateProcess && s.Risk == RiskLevel.Low);

        var btnSvc = FilterBtn("☑ Servisler", 445, 95);
        btnSvc.Click += (_, _) => SetChecked(s => (s.Type == SuggestionType.StopService || s.Type == SuggestionType.SetServiceManual) && s.Risk == RiskLevel.Low);

        var btnStart = FilterBtn("☑ Startup", 544, 90);
        btnStart.Click += (_, _) => SetChecked(s => s.Type == SuggestionType.DisableStartup && s.Risk == RiskLevel.Low);

        var btnTask = FilterBtn("☑ Tasks", 638, 90);
        btnTask.Click += (_, _) => SetChecked(s => s.Type == SuggestionType.DisableScheduledTask && s.Risk == RiskLevel.Low);

        pnlFilterToolbar.Controls.AddRange(new Control[] { btnAll, btnNone, btnSafe, btnProc, btnSvc, btnStart, btnTask });
        Controls.Add(pnlFilterToolbar);

        // Scrollable panel for suggestions
        var pnlScroll = new Panel
        {
            Location = new Point(12, 110),
            Size = new Size(780, 480),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AutoScroll = true,
            BackColor = BgCard,
        };

        // Column headers
        var pnlHeader = new Panel { Location = new Point(0, 0), Size = new Size(760, 26), BackColor = Color.FromArgb(20, 20, 20) };
        AddHeaderLabel(pnlHeader, "✔", 4, 30);
        AddHeaderLabel(pnlHeader, "Öğe Adı", 34, 220);
        AddHeaderLabel(pnlHeader, "Tür", 258, 120);
        AddHeaderLabel(pnlHeader, "RAM", 382, 90);
        AddHeaderLabel(pnlHeader, "Risk", 476, 70);
        AddHeaderLabel(pnlHeader, "Puan", 550, 40);
        AddHeaderLabel(pnlHeader, "Açıklama", 594, 160);
        pnlScroll.Controls.Add(pnlHeader);

        int y = 28;
        foreach (var suggestion in _suggestions)
        {
            var row = BuildSuggestionRow(suggestion, y);
            pnlScroll.Controls.Add(row);
            y += 36;
        }
        pnlScroll.AutoScrollMinSize = new Size(760, y + 10);

        // Action Buttons
        var btnOk = new Button
        {
            Text = "⚡ SEÇİLENLERİ OPTİMİZE ET",
            Location = new Point(480, 598),
            Size = new Size(220, 36),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            BackColor = AccentGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += BtnOk_Click;

        var btnCancel = new Button
        {
            Text = "İptal",
            Location = new Point(708, 598),
            Size = new Size(84, 36),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            BackColor = Color.FromArgb(60, 40, 40),
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat
        };
        btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Controls.AddRange(new Control[] { lblTitle, _lblTotal, _lblCounter, pnlScroll, btnOk, btnCancel });
    }

    private static Button FilterBtn(string text, int x, int w)
    {
        var b = new Button
        {
            Text = text,
            Location = new Point(x, 4),
            Size = new Size(w, 26),
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 7.5f)
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
        return b;
    }

    private static void AddHeaderLabel(Panel panel, string text, int x, int w)
    {
        panel.Controls.Add(new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(150, 150, 150),
            Location = new Point(x, 5),
            Size = new Size(w, 18)
        });
    }

    private Panel BuildSuggestionRow(OptimizationSuggestion sug, int y)
    {
        var row = new Panel
        {
            Location = new Point(0, y),
            Size = new Size(760, 34),
            BackColor = y % 2 == 0 ? Color.FromArgb(28, 28, 28) : Color.FromArgb(32, 32, 32)
        };

        var cb = new CheckBox
        {
            Location = new Point(8, 8),
            Size = new Size(20, 20),
            Checked = sug.IsAutoSelected,
            Tag = sug
        };
        cb.CheckedChanged += (_, _) => UpdateCounter();
        _checkboxes.Add(cb);

        var lblName = new Label
        {
            Text = !string.IsNullOrEmpty(sug.DisplayName) ? sug.DisplayName : sug.Name,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(220, 220, 220),
            Location = new Point(34, 8),
            Size = new Size(220, 18),
            AutoEllipsis = true
        };

        var lblType = new Label
        {
            Text = sug.TypeDisplay,
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(100, 160, 220),
            Location = new Point(258, 9),
            Size = new Size(120, 16)
        };

        var lblRam = new Label
        {
            Text = sug.EstimatedRamDisplay,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = AccentGreen,
            Location = new Point(382, 8),
            Size = new Size(88, 18)
        };

        var riskColor = sug.Risk switch
        {
            RiskLevel.Low => Color.FromArgb(16, 185, 129),
            RiskLevel.Medium => Color.FromArgb(245, 158, 11),
            RiskLevel.High => Color.FromArgb(239, 68, 68),
            _ => Color.FromArgb(239, 68, 68)
        };

        var lblRisk = new Label
        {
            Text = sug.RiskDisplay,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = riskColor,
            Location = new Point(476, 9),
            Size = new Size(68, 16)
        };

        var lblScore = new Label
        {
            Text = sug.Score.ToString(),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = AccentBlue,
            Location = new Point(550, 8),
            Size = new Size(40, 18)
        };

        var lblDesc = new Label
        {
            Text = sug.Description,
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = TextSecondary,
            Location = new Point(594, 9),
            Size = new Size(160, 16),
            AutoEllipsis = true
        };

        row.Controls.AddRange(new Control[] { cb, lblName, lblType, lblRam, lblRisk, lblScore, lblDesc });
        return row;
    }

    private void SetChecked(Func<OptimizationSuggestion, bool> predicate)
    {
        foreach (var cb in _checkboxes)
        {
            if (cb.Tag is OptimizationSuggestion s)
            {
                // Never auto-check HIGH/CRITICAL items
                cb.Checked = s.Risk < RiskLevel.High && predicate(s);
            }
        }
        UpdateCounter();
    }

    private void UpdateCounter()
    {
        int selected = _checkboxes.Count(c => c.Checked);
        int total = _checkboxes.Count;
        _lblCounter.Text = $"Seçili: {selected} / {total}";

        long totalBytes = _checkboxes
            .Where(c => c.Checked && c.Tag is OptimizationSuggestion)
            .Sum(c => ((OptimizationSuggestion)c.Tag!).EstimatedRamSaveBytes);

        double totalMB = totalBytes / (1024.0 * 1024.0);
        string totalStr = totalMB >= 1024
            ? $"{totalBytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
            : $"{totalMB:F0} MB";

        _lblTotal.Text = $"Tahmini kazanım: ~{totalStr} RAM";
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        SelectedSuggestions = _checkboxes
            .Where(c => c.Checked && c.Tag is OptimizationSuggestion)
            .Select(c =>
            {
                var sug = (OptimizationSuggestion)c.Tag!;
                sug.IsSelected = true;
                return sug;
            })
            .ToList();

        DialogResult = DialogResult.OK;
    }
}
