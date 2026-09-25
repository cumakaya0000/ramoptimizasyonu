using WinRamOptimizer.Core;
using WinRamOptimizer.Models;

namespace WinRamOptimizer.UI;

/// <summary>
/// Shows optimization suggestions with checkboxes and lets user select what to apply.
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

    public List<OptimizationSuggestion> SelectedSuggestions { get; private set; } = new();

    public OptimizationDialog(List<OptimizationSuggestion> suggestions, OptimizationEngine engine)
    {
        _suggestions = suggestions;
        _engine = engine;
        InitializeUI();
    }

    private void InitializeUI()
    {
        Text = "⚡ Optimizasyon Önerileri";
        Size = new Size(700, 600);
        MinimumSize = new Size(600, 500);
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
            Location = new Point(16, 14)
        };

        // Total RAM estimate
        long totalSaveable = _suggestions.Sum(s => s.EstimatedRamSaveBytes);
        double totalMB = totalSaveable / (1024.0 * 1024.0);
        string totalStr = totalMB >= 1024
            ? $"{totalSaveable / (1024.0 * 1024.0 * 1024.0):F1} GB"
            : $"{totalMB:F0} MB";

        var lblTotal = new Label
        {
            Text = $"Toplam tahmini kazanım: ~{totalStr} RAM",
            Font = new Font("Segoe UI", 10f),
            ForeColor = AccentGreen,
            AutoSize = true,
            Location = new Point(16, 44)
        };

        // Note
        var lblNote = new Label
        {
            Text = "⚠ HIGH ve CRITICAL risk öğeleri otomatik seçilmez. Lütfen dikkatlice inceleyiniz.",
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(245, 158, 11),
            AutoSize = false,
            Size = new Size(660, 18),
            Location = new Point(16, 68)
        };

        // Scrollable panel for suggestions
        var pnlScroll = new Panel
        {
            Location = new Point(8, 94),
            Size = new Size(670, 400),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AutoScroll = true,
            BackColor = BgCard,
        };

        // Column headers
        var pnlHeader = new Panel { Location = new Point(0, 0), Size = new Size(650, 26), BackColor = Color.FromArgb(20, 20, 20) };
        AddHeaderLabel(pnlHeader, "✔", 4, 60);
        AddHeaderLabel(pnlHeader, "Öğe", 64, 160);
        AddHeaderLabel(pnlHeader, "Tür", 224, 70);
        AddHeaderLabel(pnlHeader, "RAM", 294, 90);
        AddHeaderLabel(pnlHeader, "Risk", 384, 70);
        AddHeaderLabel(pnlHeader, "Puan", 454, 50);
        pnlScroll.Controls.Add(pnlHeader);

        int y = 28;
        foreach (var suggestion in _suggestions)
        {
            var row = BuildSuggestionRow(suggestion, y);
            pnlScroll.Controls.Add(row);
            y += 36;
        }
        pnlScroll.AutoScrollMinSize = new Size(650, y + 10);

        // Select All / None
        var btnSelectAll = new Button
        {
            Text = "Tümünü Seç",
            Location = new Point(8, 500),
            Size = new Size(120, 30),
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat
        };
        btnSelectAll.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
        btnSelectAll.Click += (_, _) => _checkboxes.ForEach(c => c.Checked = true);

        var btnSelectNone = new Button
        {
            Text = "Seçimi Kaldır",
            Location = new Point(136, 500),
            Size = new Size(120, 30),
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat
        };
        btnSelectNone.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
        btnSelectNone.Click += (_, _) => _checkboxes.ForEach(c => c.Checked = false);

        var btnOk = new Button
        {
            Text = "⚡  SEÇİLENLERİ OPTİMİZE ET",
            Location = new Point(390, 500),
            Size = new Size(240, 36),
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
            Location = new Point(638, 500),
            Size = new Size(50, 36),
            BackColor = Color.FromArgb(60, 40, 40),
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat
        };
        btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Controls.AddRange(new Control[] { lblTitle, lblTotal, lblNote, pnlScroll, btnSelectAll, btnSelectNone, btnOk, btnCancel });
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
            Size = new Size(650, 34),
            BackColor = y % 2 == 0 ? Color.FromArgb(28, 28, 28) : Color.FromArgb(32, 32, 32)
        };

        var cb = new CheckBox
        {
            Location = new Point(8, 8),
            Size = new Size(20, 20),
            Checked = sug.IsAutoSelected,
            Tag = sug
        };
        _checkboxes.Add(cb);

        var lblName = new Label
        {
            Text = sug.DisplayName.Length > 0 ? sug.DisplayName : sug.Name,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(220, 220, 220),
            Location = new Point(34, 8),
            Size = new Size(188, 18),
            AutoEllipsis = true
        };

        var lblType = new Label
        {
            Text = sug.TypeDisplay,
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(100, 160, 220),
            Location = new Point(224, 9),
            Size = new Size(68, 16)
        };

        var lblRam = new Label
        {
            Text = sug.EstimatedRamDisplay,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = AccentGreen,
            Location = new Point(294, 8),
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
            Location = new Point(384, 9),
            Size = new Size(68, 16)
        };

        var lblScore = new Label
        {
            Text = sug.Score.ToString(),
            Font = new Font("Segoe UI", 9f),
            ForeColor = AccentBlue,
            Location = new Point(454, 8),
            Size = new Size(40, 18)
        };

        row.Controls.AddRange(new Control[] { cb, lblName, lblType, lblRam, lblRisk, lblScore });
        return row;
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        SelectedSuggestions = _checkboxes
            .Where(c => c.Checked && c.Tag is OptimizationSuggestion)
            .Select(c => (OptimizationSuggestion)c.Tag!)
            .ToList();

        DialogResult = DialogResult.OK;
    }
}
