using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ChatGPTLite
{
    internal sealed class SettingsForm : Form
    {
        private readonly AppController _app;
        private readonly NumericUpDown _suspendSeconds;
        private readonly NumericUpDown _hibernateMinutes;
        private readonly NumericUpDown _cacheMb;
        private readonly CheckBox _restoreWindows;
        private readonly CheckBox _keepInTray;
        private readonly CheckBox _darkMode;
        private readonly CheckBox _capturePreview;
        private readonly CheckBox _restoreHibernatedOnlyOnClick;
        private readonly ComboBox _hibernateIndicatorStyle;
        private readonly CheckBox _automaticUpdates;
        private readonly TextBox _updateUrl;
        private readonly NumericUpDown _quickCloseMinutes;
        private readonly TextBox _quickUrl;
        private readonly CheckBox _showStartDashboard;
        private readonly NumericUpDown _startDashboardSeconds;
        private readonly Label _memoryStatus;
        private readonly System.Windows.Forms.Timer _memoryTimer;
        private bool _loadingValues;
        private bool _saved;
        private bool _originalDarkMode;

        public SettingsForm(AppController app)
        {
            _app = app;
            Text = "Options · " + AppController.ProductName;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            // Keep the compact dialog usable on 768px-tall screens. The options
            // content is longer than a typical laptop display, so make the form
            // scrollable instead of allowing its Save/Cancel buttons to fall below
            // the desktop work area.
            AutoScroll = true;
            int workHeight = Screen.PrimaryScreen == null ? 900 : Screen.PrimaryScreen.WorkingArea.Height;
            ClientSize = new Size(620, Math.Min(1030, Math.Max(600, workHeight - 80)));
            AutoScrollMinSize = new Size(620, 1040);
            Font = new Font("Segoe UI", 9f);

            Label heading = new Label();
            heading.Text = "Resource saving";
            heading.Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold);
            heading.AutoSize = true;
            heading.Location = new Point(24, 22);

            Label suspendLabel = LabelAt("Suspend an inactive window after:", 26, 78, 350);
            _suspendSeconds = NumberAt(430, 74, 110, 0, 3600);
            Label secondsSuffix = LabelAt("seconds", 545, 78, 70);

            Label hibernateLabel = LabelAt("Hibernate and free memory after:", 26, 119, 350);
            _hibernateMinutes = NumberAt(430, 115, 110, 0, 240);
            Label minutesSuffix = LabelAt("minutes", 545, 119, 70);

            Label hint = LabelAt(
                "0 disables each function. Suspend pauses CPU and timers; hibernation frees the complete web view. Caffeine windows stay awake until you turn caffeine off.",
                27, 154, 560);
            hint.Height = 48;
            hint.ForeColor = Color.FromArgb(90, 90, 90);

            _restoreWindows = new CheckBox();
            _restoreWindows.Text = "Keep saved windows between launches (open them manually from the manager)";
            _restoreWindows.AutoSize = true;
            _restoreWindows.Location = new Point(29, 211);

            _keepInTray = new CheckBox();
            _keepInTray.Text = "Keep RPM Browser Lite available near the clock when windows are closed";
            _keepInTray.AutoSize = true;
            _keepInTray.Location = new Point(29, 242);

            _darkMode = new CheckBox();
            _darkMode.Text = "Use dark mode for the RPM Browser Lite interface";
            _darkMode.AutoSize = true;
            _darkMode.Location = new Point(29, 269);
            _darkMode.CheckedChanged += DarkModeChanged;

            _capturePreview = new CheckBox();
            _capturePreview.Text = "Save a reduced full-page preview before hibernating (uses disk, not WebView memory)";
            _capturePreview.AutoSize = true;
            _capturePreview.Location = new Point(29, 296);

            _restoreHibernatedOnlyOnClick = new CheckBox();
            _restoreHibernatedOnlyOnClick.Text = "Restore hibernated windows only when I click the preview or Restore button";
            _restoreHibernatedOnlyOnClick.AutoSize = true;
            _restoreHibernatedOnlyOnClick.Location = new Point(29, 323);

            Label indicatorLabel = LabelAt("Hibernation indicator:", 26, 354, 230);
            _hibernateIndicatorStyle = new ComboBox();
            _hibernateIndicatorStyle.DropDownStyle = ComboBoxStyle.DropDownList;
            _hibernateIndicatorStyle.Location = new Point(260, 350);
            _hibernateIndicatorStyle.Width = 327;
            _hibernateIndicatorStyle.Items.AddRange(new object[]
            {
                "No border or flash",
                "Soft blue pulse (recommended)",
                "Slow navy breathing",
                "Soft cyan pulse",
                "Calm violet pulse",
                "Static navy outline"
            });

            Label quickHeading = LabelAt("Quick consultation", 24, 394, 400);
            quickHeading.Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);

            Label quickUrlLabel = LabelAt("Page opened from the tray icon:", 26, 437, 350);
            _quickUrl = new TextBox();
            _quickUrl.Location = new Point(29, 463);
            _quickUrl.Width = 558;

            Label quickCloseLabel = LabelAt("Close after no interaction:", 26, 500, 350);
            _quickCloseMinutes = NumberAt(430, 496, 110, 0, 240);
            Label quickCloseSuffix = LabelAt("minutes (0 = never)", 545, 500, 100);

            Label startHeading = LabelAt("New window start page", 24, 544, 400);
            startHeading.Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);

            _showStartDashboard = new CheckBox();
            _showStartDashboard.Text = "Show frequently used and recent pages before opening ChatGPT";
            _showStartDashboard.AutoSize = true;
            _showStartDashboard.Location = new Point(29, 582);

            Label startDelayLabel = LabelAt("Open ChatGPT automatically after:", 26, 613, 350);
            _startDashboardSeconds = NumberAt(430, 609, 110, 5, 60);
            _startDashboardSeconds.Increment = 5;
            Label startDelaySuffix = LabelAt("seconds", 545, 613, 70);
            Label startHint = LabelAt("Click any page to open it immediately. The start page is native and uses no WebView memory until you choose a page.", 29, 640, 558);
            startHint.Height = 30;
            startHint.ForeColor = Color.FromArgb(90, 90, 90);
            _showStartDashboard.CheckedChanged += delegate
            {
                _startDashboardSeconds.Enabled = _showStartDashboard.Checked;
                startDelayLabel.Enabled = _showStartDashboard.Checked;
                startDelaySuffix.Enabled = _showStartDashboard.Checked;
            };

            Label storageHeading = LabelAt("Cache and updates", 24, 680, 400);
            storageHeading.Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);

            Button extensions = new Button();
            extensions.Text = "Edge extensions…";
            extensions.AutoSize = true;
            extensions.Location = new Point(455, 675);
            extensions.Click += delegate { _app.ShowExtensions(); };

            Label cacheLabel = LabelAt("Approximate web cache limit:", 26, 727, 350);
            _cacheMb = NumberAt(430, 723, 110, 64, 4096);
            Label cacheSuffix = LabelAt("MB", 545, 727, 50);

            Label updateLabel = LabelAt("Update manifest address (optional):", 26, 769, 430);
            _updateUrl = new TextBox();
            _updateUrl.Location = new Point(29, 795);
            _updateUrl.Width = 558;

            _automaticUpdates = new CheckBox();
            _automaticUpdates.Text = "Install verified updates automatically when a secure manifest is configured";
            _automaticUpdates.AutoSize = true;
            _automaticUpdates.Location = new Point(29, 822);

            Label dataLabel = LabelAt("Local data: " + _app.Paths.DataDirectory, 29, 851, 558);
            dataLabel.AutoEllipsis = true;
            dataLabel.ForeColor = Color.FromArgb(90, 90, 90);

            Button openData = new Button();
            openData.Text = "Open data folder";
            openData.AutoSize = true;
            openData.Location = new Point(29, 985);
            openData.Click += delegate
            {
                try { Process.Start(_app.Paths.DataDirectory); }
                catch { }
            };

            Button openCaptures = new Button();
            openCaptures.Text = "Open captures";
            openCaptures.AutoSize = true;
            openCaptures.Location = new Point(144, 985);
            openCaptures.Click += delegate
            {
                try
                {
                    System.IO.Directory.CreateDirectory(_app.Paths.CapturesDirectory);
                    Process.Start(_app.Paths.CapturesDirectory);
                }
                catch { }
            };

            Button save = new Button();
            save.Text = "Save";
            save.Width = 100;
            save.Height = 32;
            save.Location = new Point(380, 984);
            save.Click += delegate { SaveSettings(); };

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.Width = 100;
            cancel.Height = 32;
            cancel.Location = new Point(487, 984);
            cancel.DialogResult = DialogResult.Cancel;

            Label memoryHeading = LabelAt("Memory", 24, 880, 400);
            memoryHeading.Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);

            _memoryStatus = LabelAt(string.Empty, 29, 912, 558);
            _memoryStatus.Height = 42;
            _memoryStatus.ForeColor = Color.FromArgb(65, 65, 65);

            Button hibernateNow = new Button();
            hibernateNow.Text = "☾ Free RAM now";
            hibernateNow.AutoSize = true;
            hibernateNow.Location = new Point(29, 944);
            hibernateNow.Click += delegate
            {
                _app.HibernateInactiveWindows();
                UpdateMemoryStatus();
            };

            Button lowMemoryPreset = new Button();
            lowMemoryPreset.Text = "Use low-memory preset";
            lowMemoryPreset.AutoSize = true;
            lowMemoryPreset.Location = new Point(150, 944);
            lowMemoryPreset.Click += delegate
            {
                _suspendSeconds.Value = 20;
                _hibernateMinutes.Value = 5;
                _cacheMb.Value = 256;
                _capturePreview.Checked = true;
                _restoreHibernatedOnlyOnClick.Checked = true;
            };

            Controls.Add(heading);
            Controls.Add(suspendLabel);
            Controls.Add(_suspendSeconds);
            Controls.Add(secondsSuffix);
            Controls.Add(hibernateLabel);
            Controls.Add(_hibernateMinutes);
            Controls.Add(minutesSuffix);
            Controls.Add(hint);
            Controls.Add(_restoreWindows);
            Controls.Add(_keepInTray);
            Controls.Add(_darkMode);
            Controls.Add(_capturePreview);
            Controls.Add(_restoreHibernatedOnlyOnClick);
            Controls.Add(indicatorLabel);
            Controls.Add(_hibernateIndicatorStyle);
            Controls.Add(quickHeading);
            Controls.Add(quickUrlLabel);
            Controls.Add(_quickUrl);
            Controls.Add(quickCloseLabel);
            Controls.Add(_quickCloseMinutes);
            Controls.Add(quickCloseSuffix);
            Controls.Add(startHeading);
            Controls.Add(_showStartDashboard);
            Controls.Add(startDelayLabel);
            Controls.Add(_startDashboardSeconds);
            Controls.Add(startDelaySuffix);
            Controls.Add(startHint);
            Controls.Add(storageHeading);
            Controls.Add(extensions);
            Controls.Add(cacheLabel);
            Controls.Add(_cacheMb);
            Controls.Add(cacheSuffix);
            Controls.Add(updateLabel);
            Controls.Add(_updateUrl);
            Controls.Add(_automaticUpdates);
            Controls.Add(dataLabel);
            Controls.Add(openData);
            Controls.Add(openCaptures);
            Controls.Add(save);
            Controls.Add(cancel);
            Controls.Add(memoryHeading);
            Controls.Add(_memoryStatus);
            Controls.Add(hibernateNow);
            Controls.Add(lowMemoryPreset);

            AcceptButton = save;
            CancelButton = cancel;
            _memoryTimer = new System.Windows.Forms.Timer();
            _memoryTimer.Interval = 2500;
            _memoryTimer.Tick += delegate { UpdateMemoryStatus(); };
            _memoryTimer.Start();
            FormClosed += delegate { _memoryTimer.Stop(); _memoryTimer.Dispose(); };
            FormClosed += delegate
            {
                if (!_saved)
                    _app.PreviewDarkMode(_originalDarkMode);
            };
            LoadValues();
            UpdateMemoryStatus();
        }

        private void UpdateMemoryStatus()
        {
            MemorySnapshot memory = _app.GetMemorySnapshot();
            _memoryStatus.Text = AppController.ProductName + " + WebView2: " + FormatMb(memory.ApplicationWorkingSetBytes) +
                " MB across " + memory.ApplicationProcessCount + " process(es).\r\n" +
                "Windows RAM available: " + FormatMb((long)Math.Min((ulong)long.MaxValue, memory.AvailablePhysicalBytes)) +
                " MB of " + FormatMb((long)Math.Min((ulong)long.MaxValue, memory.TotalPhysicalBytes)) +
                " MB (" + memory.MemoryLoadPercent + "% in use).";
        }

        private void LoadValues()
        {
            AppSettings settings = _app.State.Settings;
            _loadingValues = true;
            _suspendSeconds.Value = Clamp(settings.SuspendAfterSeconds, _suspendSeconds.Minimum, _suspendSeconds.Maximum);
            _hibernateMinutes.Value = Clamp(settings.HibernateAfterMinutes, _hibernateMinutes.Minimum, _hibernateMinutes.Maximum);
            _cacheMb.Value = Clamp(settings.CacheSizeMb, _cacheMb.Minimum, _cacheMb.Maximum);
            _restoreWindows.Checked = settings.RestoreWindows;
            _keepInTray.Checked = settings.MinimizeToTray;
            _darkMode.Checked = settings.DarkMode;
            _capturePreview.Checked = settings.CapturePreviewOnHibernate;
            _restoreHibernatedOnlyOnClick.Checked = settings.RestoreHibernatedOnlyOnClick == true;
            _hibernateIndicatorStyle.SelectedIndex = Math.Max(0, Math.Min(5, settings.HibernationIndicatorStyle));
            _quickUrl.Text = settings.QuickConsultUrl ?? string.Empty;
            _quickCloseMinutes.Value = Clamp(settings.QuickConsultCloseAfterMinutes, _quickCloseMinutes.Minimum, _quickCloseMinutes.Maximum);
            _showStartDashboard.Checked = settings.ShowStartDashboard == true;
            _startDashboardSeconds.Value = Clamp(settings.StartDashboardTimeoutSeconds, _startDashboardSeconds.Minimum, _startDashboardSeconds.Maximum);
            _updateUrl.Text = settings.UpdateManifestUrl ?? string.Empty;
            _automaticUpdates.Checked = settings.AutomaticUpdates == true;
            _originalDarkMode = settings.DarkMode;
            _loadingValues = false;
            ApplyTheme();
        }

        private void DarkModeChanged(object sender, EventArgs e)
        {
            ApplyTheme();
            if (!_loadingValues)
                _app.PreviewDarkMode(_darkMode.Checked);
        }

        public void ApplyTheme()
        {
            UiTheme.ApplyNativeTree(this, _darkMode.Checked);
            UiTheme.ApplyWindowChrome(this, _darkMode.Checked);
            _memoryStatus.ForeColor = _darkMode.Checked ? UiTheme.DarkMutedText : Color.FromArgb(65, 65, 65);
        }

        private void SaveSettings()
        {
            int oldCache = _app.State.Settings.CacheSizeMb;
            _app.State.Settings.SuspendAfterSeconds = Decimal.ToInt32(_suspendSeconds.Value);
            _app.State.Settings.HibernateAfterMinutes = Decimal.ToInt32(_hibernateMinutes.Value);
            _app.State.Settings.CacheSizeMb = Decimal.ToInt32(_cacheMb.Value);
            _app.State.Settings.RestoreWindows = _restoreWindows.Checked;
            _app.State.Settings.MinimizeToTray = _keepInTray.Checked;
            _app.State.Settings.DarkMode = _darkMode.Checked;
            _app.State.Settings.CapturePreviewOnHibernate = _capturePreview.Checked;
            _app.State.Settings.RestoreHibernatedOnlyOnClick = _restoreHibernatedOnlyOnClick.Checked;
            _app.State.Settings.HibernationIndicatorStyle = Math.Max(0, _hibernateIndicatorStyle.SelectedIndex);
            _app.State.Settings.QuickConsultUrl = _quickUrl.Text.Trim();
            _app.State.Settings.QuickConsultCloseAfterMinutes = Decimal.ToInt32(_quickCloseMinutes.Value);
            _app.State.Settings.ShowStartDashboard = _showStartDashboard.Checked;
            _app.State.Settings.StartDashboardTimeoutSeconds = Decimal.ToInt32(_startDashboardSeconds.Value);
            _app.State.Settings.AutomaticUpdates = _automaticUpdates.Checked;
            _app.State.Settings.UpdateManifestUrl = _updateUrl.Text.Trim();
            _app.SettingsChanged();
            _app.SaveNow();
            _saved = true;

            if (oldCache != _app.State.Settings.CacheSizeMb)
            {
                MessageBox.Show(
                    "The new cache limit will apply the next time you close and reopen " + AppController.ProductName + ".",
                    "Configuración guardada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            Close();
        }

        private static Label LabelAt(string text, int x, int y, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Width = width;
            label.AutoSize = false;
            label.Height = 24;
            return label;
        }

        private static NumericUpDown NumberAt(int x, int y, int width, int minimum, int maximum)
        {
            NumericUpDown number = new NumericUpDown();
            number.Location = new Point(x, y);
            number.Width = width;
            number.Minimum = minimum;
            number.Maximum = maximum;
            return number;
        }

        private static decimal Clamp(int value, decimal minimum, decimal maximum)
        {
            return Math.Min(maximum, Math.Max(minimum, value));
        }

        private static string FormatMb(long bytes)
        {
            return Math.Max(0L, bytes / (1024L * 1024L)).ToString("N0");
        }
    }
}
