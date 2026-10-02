using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IntelVibrance.Core;
using IntelVibrance.Windows;

namespace IntelVibrance.UI
{
    /// <summary>
    /// Main application window.
    /// Dark mode, GPU utility aesthetic, single Digital Vibrance slider.
    /// </summary>
    public class MainWindow : Form
    {
        // ─── Win32 for dark title bar ───────────────────────────
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int MOD_CONTROL = 0x0002;
        private const int MOD_ALT = 0x0001;
        private const int VK_V = 0x56;
        private const int HOTKEY_TOGGLE_ID = 1;
        private const int WM_HOTKEY = 0x0312;

        // ─── App State ──────────────────────────────────────────
        private VibranceController _controller;
        private List<DisplayInfo> _displays;
        private DisplayInfo _selectedDisplay;
        private bool _vibranceActive = true;
        private int _savedVibranceBeforeToggle = 50;
        private NotifyIcon _trayIcon;
        private bool _isReallyClosing = false;

        // ─── UI Controls ────────────────────────────────────────
        private Panel _headerPanel;
        private Label _titleLabel, _subtitleLabel;
        private Panel _gpuPanel;
        private Label _gpuNameLabel, _displayNameLabel, _statusLabel;
        private ComboBox _displayCombo;
        private VibranceSlider _slider;
        private Label _vibranceTitleLabel;
        private Panel _presetsPanel;
        private Panel _buttonsPanel;
        private DarkButton _applyBtn, _resetBtn;
        private CheckBox _autoStartCheck, _hotkeyCheck;
        private Label _statusBarLabel;
        private TabControl _tabControl;

        // Colors
        private static readonly Color BgDark      = Color.FromArgb(15, 15, 22);
        private static readonly Color BgCard      = Color.FromArgb(24, 24, 36);
        private static readonly Color BgCardAlt   = Color.FromArgb(28, 28, 42);
        private static readonly Color AccentBlue  = Color.FromArgb(0, 120, 215);
        private static readonly Color AccentGreen = Color.FromArgb(30, 180, 100);
        private static readonly Color AccentRed   = Color.FromArgb(220, 60, 60);
        private static readonly Color TextPrimary = Color.FromArgb(230, 230, 245);
        private static readonly Color TextSecond  = Color.FromArgb(140, 140, 170);
        private static readonly Color TextMuted   = Color.FromArgb(80, 80, 100);
        private static readonly Color BorderColor = Color.FromArgb(45, 45, 65);

        public MainWindow()
        {
            InitializeComponent();
            EnableDarkTitleBar();
            InitializeApp();
        }

        private void InitializeComponent()
        {
            // ── Form setup ──────────────────────────────────────
            Text = "Intel Vibrance";
            Size = new Size(440, 680);
            MinimumSize = new Size(400, 600);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = BgDark;
            Font = new Font("Segoe UI", 9f);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = SystemIcons.Application;

            // ── Tab control ─────────────────────────────────────
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.FlatButtons,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(120, 30),
                Font = new Font("Segoe UI", 9f),
                BackColor = BgDark,
            };
            _tabControl.DrawItem += TabControl_DrawItem;

            var mainTab = new TabPage("Vibrance")  { BackColor = BgDark };
            var diagTab = new TabPage("Diagnostics") { BackColor = BgDark };
            var logTab  = new TabPage("Log")         { BackColor = BgDark };

            _tabControl.TabPages.Add(mainTab);
            _tabControl.TabPages.Add(diagTab);
            _tabControl.TabPages.Add(logTab);

            Controls.Add(_tabControl);

            BuildMainTab(mainTab);
            BuildDiagnosticsTab(diagTab);
            BuildLogTab(logTab);

            // ── System tray ─────────────────────────────────────
            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Intel Vibrance",
                Visible = true,
                ContextMenuStrip = BuildTrayMenu()
            };
            _trayIcon.DoubleClick += (s, e) => RestoreWindow();

            // ── Form events ─────────────────────────────────────
            FormClosing += OnFormClosing;
            Resize += (s, e) =>
            {
                if (WindowState == FormWindowState.Minimized)
                    Hide();
            };
        }

        private void BuildMainTab(TabPage tab)
        {
            int y = 0;

            // ── Header card ─────────────────────────────────────
            _headerPanel = new Panel
            {
                Location = new Point(12, 12),
                Size = new Size(tab.Width - 24, 70),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            };
            _headerPanel.Paint += (s, e) => DrawCard(e.Graphics, _headerPanel, 8);

            _titleLabel = new Label
            {
                Text = "Intel Vibrance",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(16, 10),
                AutoSize = true,
                BackColor = BgDark,
            };
            _subtitleLabel = new Label
            {
                Text = "Display Color Utility",
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextSecond,
                Location = new Point(18, 42),
                AutoSize = true,
                BackColor = BgDark,
            };

            // Intel badge
            var badge = new Label
            {
                Text = "Intel iGPU",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = AccentBlue,
                Location = new Point(280, 22),
                AutoSize = true,
                BackColor = BgDark,
            };

            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_subtitleLabel);
            _headerPanel.Controls.Add(badge);
            tab.Controls.Add(_headerPanel);

            // ── GPU/Display card ─────────────────────────────────
            _gpuPanel = new Panel
            {
                Location = new Point(12, 94),
                Size = new Size(tab.Width - 24, 115),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            };
            _gpuPanel.Paint += (s, e) => DrawCard(e.Graphics, _gpuPanel, 8);
            tab.Controls.Add(_gpuPanel);

            var gpuSectionLabel = MakeLabel("DISPLAY", new Point(16, 12), 8f, TextMuted, FontStyle.Bold);
            _gpuPanel.Controls.Add(gpuSectionLabel);

            // Display selector
            _displayCombo = new ComboBox
            {
                Location = new Point(16, 28),
                Size = new Size(200, 22),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BgCardAlt,
                ForeColor = TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
            };
            _displayCombo.SelectedIndexChanged += OnDisplaySelected;
            _gpuPanel.Controls.Add(_displayCombo);

            _gpuNameLabel = MakeLabel("GPU: Detecting...", new Point(16, 58), 9f, TextPrimary);
            _displayNameLabel = MakeLabel("Monitor: Detecting...", new Point(16, 78), 8.5f, TextSecond);
            _statusLabel = MakeLabel("● Status: Initializing", new Point(16, 96), 8.5f, TextSecond);
            _gpuPanel.Controls.Add(_gpuNameLabel);
            _gpuPanel.Controls.Add(_displayNameLabel);
            _gpuPanel.Controls.Add(_statusLabel);

            // ── Vibrance control card ────────────────────────────
            var vibranceCard = new Panel
            {
                Location = new Point(12, 222),
                Size = new Size(tab.Width - 24, 140),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            };
            vibranceCard.Paint += (s, e) => DrawCard(e.Graphics, vibranceCard, 8);
            tab.Controls.Add(vibranceCard);

            _vibranceTitleLabel = MakeLabel("DIGITAL VIBRANCE", new Point(16, 12), 8f, TextMuted, FontStyle.Bold);
            vibranceCard.Controls.Add(_vibranceTitleLabel);

            var vibranceValueLabel = MakeLabel("50%", new Point(350, 12), 8f, AccentBlue, FontStyle.Bold);
            vibranceCard.Controls.Add(vibranceValueLabel);

            _slider = new VibranceSlider
            {
                Location = new Point(12, 36),
                Size = new Size(vibranceCard.Width - 24, 60),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                Value = 50,
                BackColor = BgDark,
            };
            _slider.ValueChanged += (s, val) =>
            {
                vibranceValueLabel.Text = $"{val}%";
                vibranceValueLabel.Location = new Point(vibranceCard.Width - 55, 12);
                ApplyVibrancePreview(val);
            };
            vibranceCard.Controls.Add(_slider);

            // Vibrance hint labels
            var hintMin = MakeLabel("Muted", new Point(16, 104), 7.5f, TextMuted);
            var hintNorm = MakeLabel("Normal@50%", new Point(130, 104), 7.5f, TextMuted);
            var hintMax = MakeLabel("Vivid", new Point(vibranceCard.Width - 55, 104), 7.5f, TextMuted);
            vibranceCard.Controls.Add(hintMin);
            vibranceCard.Controls.Add(hintNorm);
            vibranceCard.Controls.Add(hintMax);

            // ── Presets card ─────────────────────────────────────
            _presetsPanel = new Panel
            {
                Location = new Point(12, 374),
                Size = new Size(tab.Width - 24, 80),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            };
            _presetsPanel.Paint += (s, e) => DrawCard(e.Graphics, _presetsPanel, 8);
            tab.Controls.Add(_presetsPanel);

            var presetTitle = MakeLabel("PRESETS", new Point(16, 10), 8f, TextMuted, FontStyle.Bold);
            _presetsPanel.Controls.Add(presetTitle);

            int px = 16;
            foreach (var (name, val) in StateManager.GetPresets())
            {
                int pval = val;
                string pname = name;
                var btn = new DarkButton(AccentBlue)
                {
                    Text = name,
                    Location = new Point(px, 30),
                    Size = new Size(60, 26),
                    Font = new Font("Segoe UI", 7.5f),
                };
                btn.Click += (s, e) =>
                {
                    _slider.Value = pval;
                    ApplyVibrancePreview(pval);
                    StateManager.SavePreset(pname);
                };
                _presetsPanel.Controls.Add(btn);
                px += 65;
            }

            // ── Buttons ──────────────────────────────────────────
            _buttonsPanel = new Panel
            {
                Location = new Point(12, 466),
                Size = new Size(tab.Width - 24, 50),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                BackColor = BgDark,
            };
            tab.Controls.Add(_buttonsPanel);

            _applyBtn = new DarkButton(AccentBlue)
            {
                Text = "Apply",
                Location = new Point(0, 8),
                Size = new Size(110, 34),
            };
            _applyBtn.Click += OnApply;

            _resetBtn = new DarkButton(AccentRed)
            {
                Text = "Reset to Default",
                Location = new Point(120, 8),
                Size = new Size(130, 34),
            };
            _resetBtn.Click += OnReset;

            var toggleBtn = new DarkButton(Color.FromArgb(100, 90, 180))
            {
                Text = "Toggle Off",
                Location = new Point(260, 8),
                Size = new Size(90, 34),
            };
            toggleBtn.Click += (s, e) => OnToggle(toggleBtn);

            _buttonsPanel.Controls.Add(_applyBtn);
            _buttonsPanel.Controls.Add(_resetBtn);
            _buttonsPanel.Controls.Add(toggleBtn);

            // ── Settings ─────────────────────────────────────────
            var settingsCard = new Panel
            {
                Location = new Point(12, 528),
                Size = new Size(tab.Width - 24, 80),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            };
            settingsCard.Paint += (s, e) => DrawCard(e.Graphics, settingsCard, 8);
            tab.Controls.Add(settingsCard);

            var settingTitle = MakeLabel("SETTINGS", new Point(16, 10), 8f, TextMuted, FontStyle.Bold);
            settingsCard.Controls.Add(settingTitle);

            _autoStartCheck = new CheckBox
            {
                Text = "Start with Windows",
                ForeColor = TextPrimary,
                BackColor = BgDark,
                Location = new Point(16, 32),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                Checked = StateManager.IsAutoStartEnabled(),
            };
            _autoStartCheck.CheckedChanged += (s, e) =>
                StateManager.SetAutoStart(_autoStartCheck.Checked);
            settingsCard.Controls.Add(_autoStartCheck);

            _hotkeyCheck = new CheckBox
            {
                Text = "Ctrl+Alt+V to toggle vibrance",
                ForeColor = TextPrimary,
                BackColor = BgDark,
                Location = new Point(200, 32),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                Checked = StateManager.IsHotkeyEnabled(),
            };
            _hotkeyCheck.CheckedChanged += OnHotkeyToggle;
            settingsCard.Controls.Add(_hotkeyCheck);

            // ── Status bar ───────────────────────────────────────
            _statusBarLabel = new Label
            {
                Text = "Ready",
                ForeColor = TextSecond,
                BackColor = Color.FromArgb(10, 10, 18),
                Location = new Point(0, tab.Height - 24),
                Size = new Size(tab.Width, 22),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Font = new Font("Segoe UI", 8f),
                Padding = new Padding(10, 3, 0, 0),
            };
            tab.Controls.Add(_statusBarLabel);
        }

        private void BuildDiagnosticsTab(TabPage tab)
        {
            var diagBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = BgDark,
                ForeColor = TextPrimary,
                Font = new Font("Consolas", 9f),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(10),
            };
            tab.Controls.Add(diagBox);

            tab.Enter += (s, e) => RefreshDiagnostics(diagBox);
        }

        private void BuildLogTab(TabPage tab)
        {
            var logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(10, 10, 16),
                ForeColor = Color.FromArgb(160, 220, 160),
                Font = new Font("Consolas", 8.5f),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
            };
            tab.Controls.Add(logBox);

            var refreshBtn = new DarkButton(AccentBlue)
            {
                Text = "Refresh Log",
                Dock = DockStyle.Bottom,
                Height = 30,
            };
            refreshBtn.Click += (s, e) => logBox.Text = Logger.GetRecentLogs(100);
            tab.Controls.Add(refreshBtn);

            tab.Enter += (s, e) => logBox.Text = Logger.GetRecentLogs(100);
        }

        private void RefreshDiagnostics(RichTextBox box)
        {
            if (_displays == null) return;

            box.Clear();
            box.SelectionColor = Color.FromArgb(80, 160, 255);
            box.AppendText("═══ System Diagnostics ═══\n\n");

            box.SelectionColor = TextMuted;
            box.AppendText("Windows Version\n");
            box.SelectionColor = TextPrimary;
            box.AppendText($"  {DisplayManager.GetWindowsVersion()}\n\n");

            box.SelectionColor = TextMuted;
            box.AppendText("GPU Adapters\n");
            foreach (var (name, drv) in DisplayManager.GetGPUInfo())
            {
                bool isIntel = name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0;
                box.SelectionColor = isIntel ? Color.FromArgb(0, 180, 255) : TextSecond;
                box.AppendText($"  • {name}\n");
                box.SelectionColor = TextMuted;
                box.AppendText($"    Driver: {drv}\n");
            }
            box.AppendText("\n");

            box.SelectionColor = TextMuted;
            box.AppendText("Active Displays\n");
            foreach (var d in _displays)
            {
                box.SelectionColor = TextPrimary;
                box.AppendText($"  {d.DeviceName}\n");
                box.SelectionColor = TextMuted;
                box.AppendText($"    Adapter:   {d.FriendlyName}\n");
                box.AppendText($"    Monitor:   {d.MonitorName}\n");
                box.AppendText($"    Primary:   {(d.IsPrimary ? "Yes" : "No")}\n");
                box.AppendText($"    HDR:       {(d.IsHDR ? "Enabled ⚠" : "Disabled")}\n");
                box.AppendText($"    Vibrance:  {(d.IsHDR ? "May be limited" : "Available")}\n\n");
            }

            box.SelectionColor = TextMuted;
            box.AppendText("Color Control Method\n");
            box.SelectionColor = TextPrimary;
            box.AppendText("  SetDeviceGammaRamp (GDI32)\n");
            box.SelectionColor = TextSecond;
            box.AppendText("  Per-channel gamma LUT (256 × 16-bit per R/G/B)\n");
            box.AppendText("  Applied via CreateDC per display device\n\n");

            box.SelectionColor = Color.FromArgb(255, 180, 50);
            box.AppendText("⚠ Limitations\n");
            box.SelectionColor = TextMuted;
            box.AppendText("  • Exclusive fullscreen games may override gamma\n");
            box.AppendText("  • HDR mode: effect may not apply\n");
            box.AppendText("  • Ramp can be reset by display driver events\n");
            box.AppendText("  • Not a full color matrix (cross-channel terms limited)\n");

            box.SelectionStart = 0;
        }

        // ─────────────────────────────────────────────────────
        // App initialization
        // ─────────────────────────────────────────────────────
        private void InitializeApp()
        {
            Logger.Initialize();
            Logger.Log("[INFO] Application started");

            _controller = new VibranceController();
            _displays = DisplayManager.GetDisplays();

            Logger.Log($"[INFO] Detected {_displays.Count} display(s)");

            // Populate display combo
            _displayCombo.Items.Clear();
            foreach (var d in _displays)
                _displayCombo.Items.Add(d);

            // Select saved or primary
            string saved = StateManager.LoadSelectedDisplay();
            _selectedDisplay = _displays.FirstOrDefault(d => d.DeviceName == saved)
                            ?? _displays.FirstOrDefault(d => d.IsPrimary)
                            ?? _displays.FirstOrDefault();

            if (_selectedDisplay != null)
                _displayCombo.SelectedItem = _selectedDisplay;

            // Initialize vibrance controller
            bool initOk = _controller.Initialize(_displays);
            Logger.Log(initOk ? "[INFO] Vibrance controller initialized" : "[WARN] Vibrance controller initialization failed");

            // Load saved vibrance
            int savedVibrance = StateManager.LoadVibrance();
            _slider.Value = savedVibrance;

            // Update GPU info display
            UpdateGPUDisplay();

            // Apply vibrance on startup if configured
            if (StateManager.ShouldApplyOnStartup() && _selectedDisplay != null)
            {
                bool applied = _controller.ApplyVibrance(_selectedDisplay, savedVibrance);
                SetStatus(applied
                    ? $"● Active — {savedVibrance}% vibrance applied"
                    : "● Warning: Could not apply vibrance (may require display driver support)",
                    applied ? AccentGreen : Color.FromArgb(255, 180, 50));
            }

            // Register hotkey if enabled
            if (StateManager.IsHotkeyEnabled())
                RegisterHotKey(Handle, HOTKEY_TOGGLE_ID, MOD_CONTROL | MOD_ALT, VK_V);
        }

        private void UpdateGPUDisplay()
        {
            string intelGpu = DisplayManager.DetectIntelGPU();

            if (_selectedDisplay != null)
            {
                _gpuNameLabel.Text = $"GPU: {_selectedDisplay.FriendlyName}";
                _displayNameLabel.Text = $"Monitor: {_selectedDisplay.MonitorName}";

                bool isIntel = _selectedDisplay.FriendlyName.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0;

                if (_selectedDisplay.IsHDR)
                {
                    _statusLabel.Text = "● HDR Active — Vibrance may be limited";
                    _statusLabel.ForeColor = Color.FromArgb(255, 180, 50);
                }
                else if (isIntel)
                {
                    _statusLabel.Text = "● Digital Vibrance Available";
                    _statusLabel.ForeColor = AccentGreen;
                }
                else
                {
                    _statusLabel.Text = "● Display controlled by non-Intel GPU";
                    _statusLabel.ForeColor = TextSecond;
                }

                Logger.Log($"[INFO] Display: {_selectedDisplay.FriendlyName} | {_selectedDisplay.MonitorName} | HDR={_selectedDisplay.IsHDR}");
            }
        }

        private void ApplyVibrancePreview(int vibrance)
        {
            if (_selectedDisplay == null) return;
            _controller.ApplyVibrance(_selectedDisplay, vibrance);
            StateManager.SaveVibrance(vibrance);
            SetStatus($"● Active — {vibrance}% vibrance", AccentGreen);
        }

        private void OnApply(object sender, EventArgs e)
        {
            if (_selectedDisplay == null)
            {
                SetStatus("● No display selected", AccentRed);
                return;
            }

            int val = _slider.Value;
            bool ok = _controller.ApplyVibrance(_selectedDisplay, val);
            StateManager.SaveVibrance(val);
            StateManager.SavePreset("Custom");

            SetStatus(ok
                ? $"● Applied — {val}% vibrance"
                : "● Failed to apply — display driver may not support gamma ramp",
                ok ? AccentGreen : AccentRed);

            Logger.Log($"[INFO] User applied vibrance: {val}");
        }

        private void OnReset(object sender, EventArgs e)
        {
            _slider.Value = 50;
            if (_selectedDisplay != null)
                _controller.RestoreDisplay(_selectedDisplay);

            StateManager.SaveVibrance(50);
            SetStatus("● Reset to default (50%)", TextSecond);
            Logger.Log("[INFO] User reset vibrance to default");
        }

        private bool _toggleState = true;
        private void OnToggle(DarkButton btn)
        {
            if (_toggleState)
            {
                _savedVibranceBeforeToggle = _slider.Value;
                if (_selectedDisplay != null)
                    _controller.RestoreDisplay(_selectedDisplay);
                btn.Text = "Toggle On";
                SetStatus("● Vibrance disabled (toggled off)", TextMuted);
                _toggleState = false;
            }
            else
            {
                _slider.Value = _savedVibranceBeforeToggle;
                if (_selectedDisplay != null)
                    _controller.ApplyVibrance(_selectedDisplay, _savedVibranceBeforeToggle);
                btn.Text = "Toggle Off";
                SetStatus($"● Active — {_savedVibranceBeforeToggle}%", AccentGreen);
                _toggleState = true;
            }
        }

        private void OnDisplaySelected(object sender, EventArgs e)
        {
            if (_displayCombo.SelectedItem is DisplayInfo display)
            {
                _selectedDisplay = display;
                StateManager.SaveSelectedDisplay(display.DeviceName);
                UpdateGPUDisplay();
                _controller.ApplyVibrance(_selectedDisplay, _slider.Value);
            }
        }

        private void OnHotkeyToggle(object sender, EventArgs e)
        {
            bool enabled = _hotkeyCheck.Checked;
            StateManager.SetHotkeyEnabled(enabled);
            if (enabled)
            {
                bool ok = RegisterHotKey(Handle, HOTKEY_TOGGLE_ID, MOD_CONTROL | MOD_ALT, VK_V);
                SetStatus(ok ? "● Hotkey Ctrl+Alt+V registered" : "● Hotkey registration failed", ok ? AccentGreen : AccentRed);
            }
            else
            {
                UnregisterHotKey(Handle, HOTKEY_TOGGLE_ID);
                SetStatus("● Hotkey unregistered", TextSecond);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_TOGGLE_ID)
            {
                // Find toggle button and click it
                foreach (Control c in _buttonsPanel.Controls)
                    if (c is DarkButton db && (db.Text == "Toggle Off" || db.Text == "Toggle On"))
                    {
                        OnToggle(db);
                        break;
                    }
            }
            base.WndProc(ref m);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_isReallyClosing && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                _trayIcon.ShowBalloonTip(2000, "Intel Vibrance",
                    "Running in system tray. Double-click to restore.", ToolTipIcon.Info);
                return;
            }

            // Real close: restore display
            Logger.Log("[INFO] Application closing — restoring display state");
            if (_displays != null && _controller != null)
                _controller.RestoreAll(_displays);

            UnregisterHotKey(Handle, HOTKEY_TOGGLE_ID);
            _trayIcon.Visible = false;
        }

        private void RestoreWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
        }

        private ContextMenuStrip BuildTrayMenu()
        {
            var menu = new ContextMenuStrip
            {
                BackColor = BgCard,
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 9f),
            };
            var open = new ToolStripMenuItem("Open Intel Vibrance");
            open.Click += (s, e) => RestoreWindow();
            var exit = new ToolStripMenuItem("Exit (Restore Display)");
            exit.Click += (s, e) =>
            {
                _isReallyClosing = true;
                Close();
            };
            menu.Items.Add(open);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);
            return menu;
        }

        private void SetStatus(string text, Color color)
        {
            if (_statusBarLabel.InvokeRequired)
            {
                _statusBarLabel.Invoke(new Action(() => SetStatus(text, color)));
                return;
            }
            _statusBarLabel.Text = text;
            _statusBarLabel.ForeColor = color;
        }

        // ─────────────────────────────────────────────────────
        // Painting helpers
        // ─────────────────────────────────────────────────────
        private static void DrawCard(Graphics g, Panel panel, int radius)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
            using (var path = RoundedPath(rect, radius))
            {
                using (var brush = new SolidBrush(BgCard))
                    g.FillPath(brush, path);
                using (var pen = new Pen(BorderColor, 1f))
                    g.DrawPath(pen, path);
            }
        }

        private static GraphicsPath RoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Top, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Label MakeLabel(string text, Point loc, float size, Color color, FontStyle style = FontStyle.Regular)
        {
            return new Label
            {
                Text = text,
                Location = loc,
                AutoSize = true,
                Font = new Font("Segoe UI", size, style),
                ForeColor = color,
                BackColor = BgDark,
            };
        }

        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            var tab = _tabControl.TabPages[e.Index];
            bool selected = _tabControl.SelectedIndex == e.Index;
            e.Graphics.FillRectangle(new SolidBrush(selected ? BgCard : BgDark), e.Bounds);
            Color textColor = selected ? AccentBlue : TextSecond;
            TextRenderer.DrawText(e.Graphics, tab.Text, new Font("Segoe UI", 9f, selected ? FontStyle.Bold : FontStyle.Regular),
                e.Bounds, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (selected)
            {
                using (var pen = new Pen(AccentBlue, 2))
                    e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }


        private void EnableDarkTitleBar()
        {
            int value = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _controller?.Dispose();
                _trayIcon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

