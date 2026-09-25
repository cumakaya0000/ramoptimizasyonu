using System.Drawing.Drawing2D;
using WinRamOptimizer.Core;
using WinRamOptimizer.Models;
using WinRamOptimizer.Services;

namespace WinRamOptimizer.UI;

public partial class MainForm : Form
{
    // ─── Colors ───────────────────────────────────────────────────────────────
    private static readonly Color BgDark = Color.FromArgb(18, 18, 18);
    private static readonly Color BgCard = Color.FromArgb(30, 30, 30);
    private static readonly Color AccentBlue = Color.FromArgb(0, 120, 212);
    private static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
    private static readonly Color AccentOrange = Color.FromArgb(245, 158, 11);
    private static readonly Color AccentRed = Color.FromArgb(239, 68, 68);
    private static readonly Color TextPrimary = Color.FromArgb(240, 240, 240);
    private static readonly Color TextSecondary = Color.FromArgb(160, 160, 160);
    private static readonly Color TextMuted = Color.FromArgb(100, 100, 100);

    // ─── Core ─────────────────────────────────────────────────────────────────
    private readonly SafetyManager _safetyManager = new();
    private SystemAnalyzer? _analyzer;
    private OptimizationEngine? _engine;
    private SystemAnalysisResult? _lastAnalysis;
    private CancellationTokenSource? _analysisCts;

    // ─── RAM history for chart ─────────────────────────────────────────────────
    private readonly Queue<double> _ramHistory = new(61);
    private System.Windows.Forms.Timer? _ramTimer;

    // ─── Controls ─────────────────────────────────────────────────────────────
    private Label _lblTitle = null!;
    private Label _lblVersion = null!;
    private Panel _pnlRam = null!;
    private Label _lblRamTotal = null!;
    private Label _lblRamUsed = null!;
    private Label _lblRamFree = null!;
    private Label _lblRamPct = null!;
    private ProgressBar _pbRam = null!;
    private Panel _pnlChart = null!;
    private Panel _pnlStats = null!;
    private Label _lblProcessCount = null!;
    private Label _lblBgProcessCount = null!;
    private Label _lblServiceCount = null!;
    private Label _lblStartupCount = null!;
    private Label _lblTaskCount = null!;

    // Mode Selector & One-Click Button (Requirement 3 & 14)
    private ComboBox _cboMode = null!;
    private Label _lblModeDesc = null!;
    private Button _btnOneClickClean = null!;
    private Button _btnAnalyze = null!;
    private Button _btnOptimize = null!;
    private Button _btnRestore = null!;

    private ProgressBar _pbProgress = null!;
    private Label _lblStatus = null!;

    public MainForm()
    {
        InitializeComponents();
        SetupTimers();

        _analyzer = new SystemAnalyzer(_safetyManager);
        _engine = new OptimizationEngine(_safetyManager);

        // Initial RAM read
        UpdateRamDisplay();

        LogService.Info("WinRam Optimizer V2 started.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  UI CONSTRUCTION
    // ──────────────────────────────────────────────────────────────────────────

    private void InitializeComponents()
    {
        SuspendLayout();
        Text = "WinRam Optimizer V2";
        Size = new Size(980, 780);
        MinimumSize = new Size(860, 680);
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;

        BuildLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private void BuildLayout()
    {
        // ─── Title bar ────────────────────────────────────────────────────────
        var pnlHeader = CreateCard(new Rectangle(0, 0, 980, 56));
        pnlHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pnlHeader.BackColor = Color.FromArgb(12, 12, 12);

        _lblTitle = new Label
        {
            Text = "⚡ WinRam Optimizer V2",
            Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            ForeColor = AccentBlue,
            AutoSize = true,
            Location = new Point(16, 14)
        };
        _lblVersion = new Label
        {
            Text = "v2.0 Pro  |  Windows 10/11",
            Font = new Font("Segoe UI", 8f),
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(260, 20)
        };

        // Navigation buttons
        var btnProcesses = NavButton("🔧 Processler", 610);
        var btnServices = NavButton("⚙️ Servisler", 705);
        var btnStartup = NavButton("🚀 Başlangıç", 800);
        var btnSettings = NavButton("⚙ Ayarlar", 892);

        btnProcesses.Click += (_, _) => OpenProcesses();
        btnServices.Click += (_, _) => OpenServices();
        btnStartup.Click += (_, _) => OpenStartup();
        btnSettings.Click += (_, _) => OpenSettings();

        pnlHeader.Controls.AddRange(new Control[] { _lblTitle, _lblVersion, btnProcesses, btnServices, btnStartup, btnSettings });
        Controls.Add(pnlHeader);

        // ─── RAM card ─────────────────────────────────────────────────────────
        _pnlRam = CreateCard(new Rectangle(12, 68, 480, 180));
        _pnlRam.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        BuildRamCard();
        Controls.Add(_pnlRam);

        // ─── Chart card ──────────────────────────────────────────────────────
        _pnlChart = CreateCard(new Rectangle(504, 68, 460, 180));
        _pnlChart.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _pnlChart.Paint += PnlChart_Paint;
        var lblChartTitle = new Label
        {
            Text = "📈 Son 60 Saniye RAM Kullanımı",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(12, 10)
        };
        _pnlChart.Controls.Add(lblChartTitle);
        Controls.Add(_pnlChart);

        // ─── Stats card ───────────────────────────────────────────────────────
        _pnlStats = CreateCard(new Rectangle(12, 260, 952, 90));
        _pnlStats.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        BuildStatsCard();
        Controls.Add(_pnlStats);

        // ─── Mode Selector Panel (Requirement 14) ────────────────────────────
        var pnlMode = CreateCard(new Rectangle(12, 360, 952, 54));
        pnlMode.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        var lblModeTitle = new Label
        {
            Text = "🎯 Temizlik Modu:",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(16, 16)
        };

        _cboMode = new ComboBox
        {
            Location = new Point(135, 12),
            Size = new Size(130, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(42, 42, 42),
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat
        };
        _cboMode.Items.AddRange(new[] { "Güvenli", "Agresif", "Manuel" });
        _cboMode.SelectedIndex = 0; // Default: Güvenli
        _cboMode.SelectedIndexChanged += CboMode_SelectedIndexChanged;

        _lblModeDesc = new Label
        {
            Text = "Güvenli Mod: Sadece kesin olarak güvenli olduğu bilinen kullanıcı uygulamalarını ve updater bileşenlerini temizler.",
            Font = new Font("Segoe UI", 8f),
            ForeColor = AccentGreen,
            AutoSize = false,
            Size = new Size(660, 32),
            Location = new Point(275, 14)
        };

        pnlMode.Controls.AddRange(new Control[] { lblModeTitle, _cboMode, _lblModeDesc });
        Controls.Add(pnlMode);

        // ─── Action Buttons Panel ─────────────────────────────────────────────
        // Requirement 3: Big TRUE ONE-CLICK RAM CLEAN button
        _btnOneClickClean = CreateMainButton("⚡ TEK TIK RAM TEMİZLE", AccentGreen, new Rectangle(12, 424, 380, 56));
        _btnOneClickClean.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _btnOneClickClean.Click += BtnOneClickClean_Click;
        Controls.Add(_btnOneClickClean);

        _btnAnalyze = CreateMainButton("🔍 SİSTEMİ ANALİZ ET", AccentBlue, new Rectangle(402, 424, 180, 56));
        _btnAnalyze.Click += BtnAnalyze_Click;
        Controls.Add(_btnAnalyze);

        _btnOptimize = CreateMainButton("⚡ DETAYLI İNCELE", Color.FromArgb(0, 100, 180), new Rectangle(592, 424, 180, 56));
        _btnOptimize.Enabled = false;
        _btnOptimize.Click += BtnOptimize_Click;
        Controls.Add(_btnOptimize);

        _btnRestore = CreateMainButton("↩ GERİ AL", Color.FromArgb(100, 50, 50), new Rectangle(782, 424, 182, 56));
        _btnRestore.Click += BtnRestore_Click;
        Controls.Add(_btnRestore);

        // ─── Progress bar & status ────────────────────────────────────────────
        _pbProgress = new ProgressBar
        {
            Location = new Point(12, 492),
            Size = new Size(952, 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Style = ProgressBarStyle.Continuous,
            ForeColor = AccentBlue,
            BackColor = Color.FromArgb(40, 40, 40),
            Visible = false
        };
        Controls.Add(_pbProgress);

        _lblStatus = new Label
        {
            Text = "Hazır. '⚡ TEK TIK RAM TEMİZLE' butonuna basarak optimizasyonu başlatabilirsiniz.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(12, 508)
        };
        Controls.Add(_lblStatus);

        // ─── Log area ─────────────────────────────────────────────────────────
        var pnlLog = CreateCard(new Rectangle(12, 535, 952, 195));
        pnlLog.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

        var lblLogTitle = new Label
        {
            Text = "📋 Aktivite & Optimizasyon Raporu",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(12, 8)
        };

        var rtbLog = new RichTextBox
        {
            Location = new Point(8, 28),
            Size = new Size(936, 158),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(18, 18, 18),
            ForeColor = TextSecondary,
            Font = new Font("Consolas", 8.5f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        rtbLog.Text = LogService.ReadLogs(50);
        rtbLog.SelectionStart = rtbLog.TextLength;
        rtbLog.ScrollToCaret();
        pnlLog.Controls.AddRange(new Control[] { lblLogTitle, rtbLog });
        Controls.Add(pnlLog);
    }

    private void BuildRamCard()
    {
        var lblRamTitle = new Label
        {
            Text = "💾 RAM Kullanımı",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(12, 10)
        };

        _lblRamTotal = new Label
        {
            Text = "Toplam: –",
            Font = new Font("Segoe UI", 9f),
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(12, 36)
        };

        _lblRamUsed = new Label
        {
            Text = "Kullanılan: –",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = AccentBlue,
            AutoSize = true,
            Location = new Point(12, 60)
        };

        _lblRamFree = new Label
        {
            Text = "Boş: –",
            Font = new Font("Segoe UI", 9f),
            ForeColor = AccentGreen,
            AutoSize = true,
            Location = new Point(12, 96)
        };

        _pbRam = new ProgressBar
        {
            Location = new Point(12, 124),
            Size = new Size(456, 16),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 100
        };

        _lblRamPct = new Label
        {
            Text = "%0",
            Font = new Font("Segoe UI", 20f, FontStyle.Bold),
            ForeColor = AccentBlue,
            AutoSize = true,
            Location = new Point(360, 50)
        };

        _pnlRam.Controls.AddRange(new Control[]
        {
            lblRamTitle, _lblRamTotal, _lblRamUsed, _lblRamFree, _pbRam, _lblRamPct
        });
    }

    private void BuildStatsCard()
    {
        var stats = new (string Icon, string Label, Label Ref)[]
        {
            ("🔄", "Processler", _lblProcessCount = StatLabel()),
            ("🔲", "Arka Plan", _lblBgProcessCount = StatLabel()),
            ("⚙️", "Servisler", _lblServiceCount = StatLabel()),
            ("🚀", "Başlangıç", _lblStartupCount = StatLabel()),
            ("📅", "Tasks", _lblTaskCount = StatLabel()),
        };

        int x = 16;
        foreach (var (icon, label, lbl) in stats)
        {
            var grp = new Panel
            {
                Location = new Point(x, 8),
                Size = new Size(175, 74),
                BackColor = Color.Transparent
            };

            var lblIcon = new Label { Text = icon, Font = new Font("Segoe UI", 16f), AutoSize = true, Location = new Point(0, 8), ForeColor = AccentBlue };
            var lblName = new Label { Text = label, Font = new Font("Segoe UI", 8f), ForeColor = TextMuted, AutoSize = true, Location = new Point(36, 8) };
            lbl.Location = new Point(36, 28);
            lbl.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            lbl.ForeColor = TextPrimary;
            lbl.Text = "–";

            grp.Controls.AddRange(new Control[] { lblIcon, lblName, lbl });
            _pnlStats.Controls.Add(grp);
            x += 185;
        }
    }

    private static Label StatLabel() => new() { AutoSize = true };

    private static Panel CreateCard(Rectangle bounds)
    {
        return new Panel
        {
            Location = bounds.Location,
            Size = bounds.Size,
            BackColor = BgCard,
            Padding = new Padding(4),
        };
    }

    private static Button NavButton(string text, int x)
    {
        return new Button
        {
            Text = text,
            Font = new Font("Segoe UI", 8f),
            ForeColor = TextSecondary,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(88, 30),
            Location = new Point(x, 12),
            Cursor = Cursors.Hand
        };
    }

    private static Button CreateMainButton(string text, Color accent, Rectangle bounds)
    {
        var btn = new Button
        {
            Text = text,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = accent,
            FlatStyle = FlatStyle.Flat,
            Location = bounds.Location,
            Size = bounds.Size,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void CboMode_SelectedIndexChanged(object? sender, EventArgs e)
    {
        switch (_cboMode.SelectedIndex)
        {
            case 0: // Safe
                _lblModeDesc.Text = "Güvenli Mod: Sadece kesin olarak güvenli olduğu bilinen kullanıcı uygulamalarını ve updater bileşenlerini temizler.";
                _lblModeDesc.ForeColor = AccentGreen;
                break;
            case 1: // Aggressive
                _lblModeDesc.Text = "Agresif Mod: Kullanılmayan arka plan uygulamalarını, opsiyonel servisleri ve otomatik başlangıç öğelerini kapsamlı şekilde temizler. Windows'un kritik bileşenlerine dokunmaz.";
                _lblModeDesc.ForeColor = AccentOrange;
                break;
            case 2: // Manual
                _lblModeDesc.Text = "Manuel Mod: Tüm optimizasyon adaylarını checkbox listesinde gösterir. Kararları siz verirsiniz.";
                _lblModeDesc.ForeColor = AccentBlue;
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  TIMERS & CHART
    // ──────────────────────────────────────────────────────────────────────────

    private void SetupTimers()
    {
        _ramTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _ramTimer.Tick += (_, _) =>
        {
            UpdateRamDisplay();
            _pnlChart.Invalidate();
        };
        _ramTimer.Start();
    }

    private void UpdateRamDisplay()
    {
        if (!IsHandleCreated) return;
        var info = MemoryService.GetRamInfo();

        InvokeIfRequired(() =>
        {
            _lblRamTotal.Text = $"Toplam: {info.TotalGB:F1} GB";
            _lblRamUsed.Text = $"Kullanılan: {info.UsedGB:F2} GB";
            _lblRamFree.Text = $"Boş: {info.FreeGB:F2} GB";
            _lblRamPct.Text = $"%{info.UsagePercent:F0}";

            var pct = (int)info.UsagePercent;
            _pbRam.Value = Math.Clamp(pct, 0, 100);

            _lblRamPct.ForeColor = pct switch
            {
                < 60 => AccentGreen,
                < 80 => AccentOrange,
                _ => AccentRed
            };
        });

        while (_ramHistory.Count >= 60) _ramHistory.Dequeue();
        _ramHistory.Enqueue(info.UsagePercent);
    }

    private void PnlChart_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var pts = _ramHistory.ToArray();
        if (pts.Length < 2) return;

        var chartRect = new Rectangle(10, 30, _pnlChart.Width - 20, _pnlChart.Height - 50);
        if (chartRect.Width <= 0 || chartRect.Height <= 0) return;

        using var gridPen = new Pen(Color.FromArgb(40, 255, 255, 255), 1);
        for (int i = 0; i <= 4; i++)
        {
            int y = chartRect.Top + chartRect.Height * i / 4;
            g.DrawLine(gridPen, chartRect.Left, y, chartRect.Right, y);
            g.DrawString($"{100 - i * 25}%", new Font("Segoe UI", 7f), Brushes.Gray,
                chartRect.Left - 2, y - 7, new StringFormat { Alignment = StringAlignment.Far });
        }

        float stepX = (float)chartRect.Width / Math.Max(1, pts.Length - 1);
        var points = new PointF[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            float x = chartRect.Left + i * stepX;
            float y = chartRect.Bottom - (float)(pts[i] / 100.0 * chartRect.Height);
            points[i] = new PointF(x, y);
        }

        var fillPts = new List<PointF>(points) { new(points[^1].X, chartRect.Bottom), new(points[0].X, chartRect.Bottom) };
        using var fillBrush = new LinearGradientBrush(chartRect, Color.FromArgb(60, 0, 120, 212), Color.Transparent, 90f);
        g.FillPolygon(fillBrush, fillPts.ToArray());

        using var linePen = new Pen(AccentBlue, 2f);
        g.DrawLines(linePen, points);

        if (pts.Length > 0)
        {
            var last = points[^1];
            g.FillEllipse(Brushes.White, last.X - 3, last.Y - 3, 6, 6);
        }

        g.DrawString("60s", new Font("Segoe UI", 7f), Brushes.Gray, chartRect.Left, chartRect.Bottom + 2);
        g.DrawString("0s", new Font("Segoe UI", 7f), Brushes.Gray, chartRect.Right - 14, chartRect.Bottom + 2);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  REQUIREMENT 3: TRUE ONE-CLICK RAM OPTIMIZATION
    // ──────────────────────────────────────────────────────────────────────────

    private async void BtnOneClickClean_Click(object? sender, EventArgs e)
    {
        _analysisCts?.Cancel();
        _analysisCts = new CancellationTokenSource();
        var ct = _analysisCts.Token;

        var selectedMode = (OptimizationMode)_cboMode.SelectedIndex;

        SetBusy(true);

        try
        {
            // 1. Analyze
            var progress = new Progress<AnalysisProgress>(p =>
            {
                InvokeIfRequired(() =>
                {
                    _pbProgress.Value = Math.Clamp((int)(p.Percent * 0.3), 0, 100);
                    _lblStatus.Text = $"[Analiz] {p.Step}";
                });
            });

            _lastAnalysis = await _analyzer!.RunFullAnalysisAsync(progress, ct);
            UpdateStatsDisplay(_lastAnalysis);

            // 2. Build suggestions
            var suggestions = _engine!.BuildSuggestions(_lastAnalysis, selectedMode);

            if (!suggestions.Any(s => s.IsAutoSelected))
            {
                MessageBox.Show("Seçili mod için herhangi bir temizlik önerisi bulunamadı. Sisteminiz optimize görünüyor.",
                    "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // If mode is Manual, open dialog
            if (selectedMode == OptimizationMode.Manual)
            {
                using var dlg = new OptimizationDialog(suggestions, _engine);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                suggestions = dlg.SelectedSuggestions;
            }

            // 3. System Restore Point (optional warning)
            if (!_engine.CreateWindowsRestorePoint(out var rpError))
            {
                LogService.Warning($"System Restore Point skipped: {rpError}");
            }

            // 4. Batch Execution with progress
            var optProgress = new Progress<OptimizationProgress>(p =>
            {
                InvokeIfRequired(() =>
                {
                    _pbProgress.Value = Math.Clamp(30 + (int)(p.Percent * 0.7), 0, 100);
                    _lblStatus.Text = $"[Temizlik] {p.Message}";
                });
            });

            var result = await _engine.ExecuteBatchAsync(suggestions, selectedMode, optProgress, ct);

            // 5. Requirement 13: Detailed Result Report
            InvokeIfRequired(() =>
            {
                UpdateRamDisplay();
                ShowDetailedResultReport(result);
            });
        }
        catch (OperationCanceledException)
        {
            InvokeIfRequired(() => _lblStatus.Text = "Optimizasyon iptal edildi.");
        }
        catch (Exception ex)
        {
            LogService.Error("One-click optimization failed", ex);
            MessageBox.Show($"Optimizasyon sırasında hata oluştu:\n{ex.Message}", "Hata",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            InvokeIfRequired(() => SetBusy(false));
        }
    }

    private async void BtnAnalyze_Click(object? sender, EventArgs e)
    {
        _analysisCts?.Cancel();
        _analysisCts = new CancellationTokenSource();
        var ct = _analysisCts.Token;

        SetBusy(true);

        try
        {
            var progress = new Progress<AnalysisProgress>(p =>
            {
                InvokeIfRequired(() =>
                {
                    _pbProgress.Value = Math.Clamp(p.Percent, 0, 100);
                    _lblStatus.Text = p.Step;
                });
            });

            _lastAnalysis = await _analyzer!.RunFullAnalysisAsync(progress, ct);

            InvokeIfRequired(() =>
            {
                UpdateStatsDisplay(_lastAnalysis);
                _btnOptimize.Enabled = true;
                _lblStatus.Text = $"✅ Analiz tamamlandı – {DateTime.Now:HH:mm:ss}";
            });
        }
        catch (OperationCanceledException)
        {
            InvokeIfRequired(() => _lblStatus.Text = "Analiz iptal edildi.");
        }
        catch (Exception ex)
        {
            LogService.Error("Analysis failed", ex);
            MessageBox.Show($"Analiz sırasında hata oluştu:\n{ex.Message}", "Hata",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            InvokeIfRequired(() => SetBusy(false));
        }
    }

    private async void BtnOptimize_Click(object? sender, EventArgs e)
    {
        if (_lastAnalysis == null)
        {
            MessageBox.Show("Önce sistem analizi yapın.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var mode = (OptimizationMode)_cboMode.SelectedIndex;
        var suggestions = _engine!.BuildSuggestions(_lastAnalysis, mode);

        using var dlg = new OptimizationDialog(suggestions, _engine);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var selected = dlg.SelectedSuggestions;
        if (!selected.Any()) return;

        SetBusy(true);

        try
        {
            var optProgress = new Progress<OptimizationProgress>(p =>
                InvokeIfRequired(() =>
                {
                    _pbProgress.Value = Math.Clamp(p.Percent, 0, 100);
                    _lblStatus.Text = p.Message;
                }));

            var result = await _engine.ExecuteBatchAsync(selected, mode, optProgress, CancellationToken.None);

            InvokeIfRequired(() =>
            {
                UpdateRamDisplay();
                ShowDetailedResultReport(result);
            });
        }
        catch (Exception ex)
        {
            LogService.Error("Optimization failed", ex);
            MessageBox.Show($"Optimizasyon sırasında hata:\n{ex.Message}", "Hata",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            InvokeIfRequired(() => SetBusy(false));
        }
    }

    private async void BtnRestore_Click(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show(
            "Son optimizasyon değişiklikleri geri alınacak.\nDevam etmek istiyor musunuz?",
            "Geri Al", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        SetBusy(true);
        _lblStatus.Text = "Değişiklikler geri alınıyor...";

        try
        {
            var (ok, errors) = await _engine!.RestoreLastSnapshotAsync();
            var msg = ok ? "Değişiklikler başarıyla geri alındı." : $"Bazı hatalar oluştu:\n{string.Join("\n", errors)}";
            MessageBox.Show(msg, ok ? "Başarılı" : "Kısmi Hata",
                MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Geri alma başarısız:\n{ex.Message}", "Hata",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            _lblStatus.Text = "Geri alma tamamlandı.";
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  RESULT REPORT (REQUIREMENT 13)
    // ──────────────────────────────────────────────────────────────────────────

    private static void ShowDetailedResultReport(OptimizationResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("╔═════════════════════════════════════════════╗");
        sb.AppendLine("║        RAM OPTİMİZASYONU TAMAMLANDI         ║");
        sb.AppendLine("╠═════════════════════════════════════════════╣");
        sb.AppendLine($"║ ÖNCE:   {result.RamBeforeGB:F2} GB (%{result.UsagePercentBefore:F0})");
        sb.AppendLine($"║ SONRA:  {result.RamAfterGB:F2} GB (%{result.UsagePercentAfter:F0})");
        sb.AppendLine($"║ GERÇEK KAZANÇ: ~{result.RamSavedDisplay}");
        sb.AppendLine("╠═════════════════════════════════════════════╣");
        sb.AppendLine($"║ Kapatılan Process:       {result.ProcessesTerminated}");
        sb.AppendLine($"║ Durdurulan Servis:       {result.ServicesStopped}");
        sb.AppendLine($"║ Manuel Yapılan Servis:   {result.ServicesSetToManual}");
        sb.AppendLine($"║ Devre Dışı Startup:      {result.StartupItemsDisabled}");
        sb.AppendLine($"║ Devre Dışı Task (Görev): {result.ScheduledTasksDisabled}");
        sb.AppendLine($"║ Başarısız İşlem:        {result.Errors.Count}");
        sb.AppendLine($"║ Tekrar Başlayan:         {result.RestartedProcessCount}");

        if (result.RestartedProcessNames.Any())
        {
            sb.AppendLine("╠═════════════════════════════════════════════╣");
            sb.AppendLine("║ Tekrar Başlayan Processler:");
            foreach (var r in result.RestartedProcessNames.Take(3))
                sb.AppendLine($"║  ⚠️ {r}");
        }

        if (result.Errors.Any())
        {
            sb.AppendLine("╠═════════════════════════════════════════════╣");
            sb.AppendLine("║ Hatalar:");
            foreach (var err in result.Errors.Take(3))
                sb.AppendLine($"║  ❌ {err[..Math.Min(err.Length, 35)]}");
        }
        sb.AppendLine("╚═════════════════════════════════════════════╝");

        MessageBox.Show(sb.ToString(), "Optimizasyon Sonucu",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UpdateStatsDisplay(SystemAnalysisResult analysis)
    {
        _lblProcessCount.Text = analysis.TotalProcessCount.ToString();
        _lblBgProcessCount.Text = analysis.BackgroundProcessCount.ToString();
        _lblServiceCount.Text = $"{analysis.RunningServiceCount}/{analysis.ServiceCount}";
        _lblStartupCount.Text = analysis.StartupCount.ToString();
        _lblTaskCount.Text = analysis.ScheduledTaskCount.ToString();
    }

    private void SetBusy(bool busy)
    {
        _btnOneClickClean.Enabled = !busy;
        _btnAnalyze.Enabled = !busy;
        _btnOptimize.Enabled = !busy && _lastAnalysis != null;
        _cboMode.Enabled = !busy;
        _pbProgress.Visible = busy;
        if (!busy) _pbProgress.Value = 0;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  NAVIGATION
    // ──────────────────────────────────────────────────────────────────────────

    private void OpenProcesses() => new ProcessesForm(_safetyManager, _lastAnalysis?.Processes).ShowDialog(this);
    private void OpenServices() => new ServicesForm(_safetyManager, _lastAnalysis?.Services).ShowDialog(this);
    private void OpenStartup() => new StartupForm(_safetyManager, _lastAnalysis?.StartupItems).ShowDialog(this);
    private void OpenSettings() => new SettingsForm().ShowDialog(this);

    private void InvokeIfRequired(Action action)
    {
        if (InvokeRequired) Invoke(action);
        else action();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _analysisCts?.Cancel();
        _analysisCts?.Dispose();
        _ramTimer?.Stop();
        _ramTimer?.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _pnlChart?.Invalidate();
    }
}
