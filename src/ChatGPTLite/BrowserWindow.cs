using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChatGPTLite
{
    internal sealed class BrowserWindow : Form
    {
        private readonly AppController _app;
        private readonly Panel _webArea;
        private readonly Panel _toolbar;
        private readonly Panel _browserHost;
        private readonly Panel _sleepPanel;
        private Panel _startPanel;
        private FlowLayoutPanel _startFrequentTiles;
        private FlowLayoutPanel _startRecentTiles;
        private Label _startCountdownLabel;
        private Label _startFrequentHeading;
        private Label _startRecentHeading;
        private TextBox _startSearchBox;
        private Button _startStopButton;
        private readonly TableLayoutPanel _sleepLayout;
        private readonly FlowLayoutPanel _restoreRow;
        private readonly Label _sleepExplanation;
        private readonly Button _restoreButton;
        private readonly PictureBox _sleepPreview;
        private readonly Panel _bookmarksPanel;
        private readonly TreeView _bookmarksTree;
        private readonly Panel _bookmarksHeader;
        private readonly Label _bookmarksHeading;
        private readonly Button _bookmarksCloseButton;
        private readonly Panel _notesPanel;
        private readonly RichTextBox _notesEditor;
        private readonly FlowLayoutPanel _noteFormatBar;
        private readonly Label _notesHeading;
        private readonly Label _notesFooter;
        private readonly Panel _notesHeader;
        private readonly Button _notesCloseButton;
        private readonly Label _sleepTitle;
        private readonly Label _statusLabel;
        private readonly Button _backButton;
        private readonly Button _forwardButton;
        private readonly Button _reloadButton;
        private readonly Button _downloadsButton;
        private readonly Button _copyModeButton;
        private readonly Button _cloneButton;
        private readonly Button _newBookmarkButton;
        private readonly Button _appearanceButton;
        private readonly Button _notesButton;
        private readonly Button _bookmarksButton;
        private readonly Button _sleepButton;
        private readonly Button _caffeineButton;
        private readonly Button _trayShortcutButton;
        private readonly Button _menuButton;
        private readonly TextBox _quickIdentifierBox;
        private readonly Button _quickColorButton;
        private readonly Button _urlDragButton;
        private readonly TextBox _addressBox;
        private readonly Button _addressGoButton;
        private readonly List<Button> _toolbarButtons;
        private readonly List<Control> _toolbarItems;
        private readonly List<Panel> _toolbarSeparators;
        private readonly Dictionary<Button, string> _toolbarGlyphs;
        private readonly System.Windows.Forms.Timer _resourceTimer;
        private readonly System.Windows.Forms.Timer _caffeinePulseTimer;
        private readonly System.Windows.Forms.Timer _hibernatePulseTimer;
        private System.Windows.Forms.Timer _startDashboardTimer;
        private readonly ToolTip _toolTip;
        private List<Image> _startDashboardImages;
        private readonly HashSet<string> _startFaviconRequests;
        private readonly AutoCompleteStringCollection _addressSuggestionTexts;
        private readonly Dictionary<string, string> _addressSuggestionUrls;
        // The native start page remains inexpensive even with many tiles: only two
        // favicon downloads can run at once and all results are stored on disk.
        private static readonly SemaphoreSlim StartFaviconDownloadSlots = new SemaphoreSlim(2, 2);
        private static readonly Color[] QuickColors = new Color[]
        {
            Color.FromArgb(220, 56, 67), Color.FromArgb(239, 119, 44), Color.FromArgb(245, 181, 42),
            Color.FromArgb(182, 201, 48), Color.FromArgb(74, 171, 94), Color.FromArgb(34, 177, 154),
            Color.FromArgb(38, 160, 210), Color.FromArgb(62, 112, 201), Color.FromArgb(104, 84, 194),
            Color.FromArgb(163, 76, 184), Color.FromArgb(222, 72, 142), Color.FromArgb(189, 91, 55),
            Color.FromArgb(95, 115, 133), Color.FromArgb(65, 80, 96), Color.FromArgb(36, 36, 42),
            Color.FromArgb(116, 84, 56), Color.FromArgb(35, 136, 126), Color.FromArgb(75, 115, 184)
        };
        private WebView2 _webView;
        private Task _initializationTask;
        private DateTime _inactiveSinceUtc;
        private bool _isHibernated;
        private bool _isSuspended;
        private bool _isHibernating;
        private bool _suspendAttemptInProgress;
        private bool _closingForExit;
        private bool _noteDocumentDirty;
        private bool _quickInitialRecoveryScheduled;
        private bool _caffeinePulsePhase;
        private bool _hibernatePulsePhase;
        private bool _copyModeEnabled;
        private bool _startDashboardVisible;
        private bool _startAddressEditPrepared;
        private int _startDashboardSecondsRemaining;
        private bool _faviconCaptureInProgress;
        private string _lastTitle;
        private Icon _presentationIcon;

        public WindowRecord Record { get; private set; }
        public CoreWebView2 CoreWebView2 { get { return _webView == null ? null : _webView.CoreWebView2; } }
        public string PageTitle { get { return _lastTitle; } }
        public string DisplayTitle { get { return Record.DisplayTitle(_lastTitle); } }
        public bool HasNotes { get { return !string.IsNullOrWhiteSpace(Record.NoteText) || Record.NoteImages.Count > 0 || _app.NoteDocumentExists(Record); } }
        public bool IsSuspended { get { return _isSuspended; } }
        public bool IsCaffeinated { get { return Record.CaffeineMode; } }
        public bool IsLockedHibernated { get { return Record.KeepHibernated; } }

        protected override bool ShowWithoutActivation
        {
            get { return _isHibernated; }
        }

        public BrowserWindow(AppController app, WindowRecord record, bool startHibernated)
        {
            _app = app;
            Record = record;
            Record.RepairPresentationDefaults();
            _lastTitle = string.IsNullOrWhiteSpace(record.Title) ? "Browser window" : record.Title;
            _isHibernated = startHibernated || record.Hibernated;
            _inactiveSinceUtc = DateTime.UtcNow;

            Text = WindowTitle();
            StartPosition = FormStartPosition.Manual;
            MinimumSize = record.IsQuickConsult ? new Size(560, 420) : new Size(560, 460);
            Size = new Size(record.Width, record.Height);
            Location = new Point(record.X, record.Y);
            if (record.IsQuickConsult)
            {
                FormBorderStyle = FormBorderStyle.SizableToolWindow;
                ShowIcon = false;
                ShowInTaskbar = false;
                MaximizeBox = false;
                MinimizeBox = false;
                TopMost = true;
                WindowState = FormWindowState.Normal;
            }
            if (record.Maximized && !record.IsQuickConsult)
                WindowState = FormWindowState.Maximized;
            KeyPreview = true;
            BackColor = Color.FromArgb(245, 245, 242);

            _toolbar = new Panel();
            _toolbar.Dock = DockStyle.Top;
            _toolbar.Height = 52;
            _toolbar.Padding = new Padding(4, 3, 4, 3);
            _toolbarButtons = new List<Button>();
            _toolbarItems = new List<Control>();
            _toolbarSeparators = new List<Panel>();
            _toolbarGlyphs = new Dictionary<Button, string>();
            _toolbar.MouseDown += delegate { MarkUserActivity(); };

            _appearanceButton = CreateToolbarButton(IconVisuals.GetGlyph(Record), 34);
            _quickIdentifierBox = new TextBox();
            _quickIdentifierBox.BorderStyle = BorderStyle.FixedSingle;
            _quickIdentifierBox.Font = new Font("Segoe UI", 8.5f);
            _quickIdentifierBox.Width = 48;
            _quickIdentifierBox.Height = 25;
            _quickIdentifierBox.TextAlign = HorizontalAlignment.Center;
            _quickIdentifierBox.Text = CompactIdentifierText(Record.Identifier);
            _quickIdentifierBox.AccessibleName = "Quick window identifier";
            _quickColorButton = CreateToolbarButton("●", 30);
            _quickColorButton.Height = 28;
            _quickColorButton.FlatStyle = FlatStyle.Flat;
            _quickColorButton.FlatAppearance.BorderSize = 1;
            _quickColorButton.AccessibleName = "Quick window color";
            _urlDragButton = CreateToolbarButton("🔗", 28);
            _urlDragButton.Height = 28;
            _urlDragButton.FlatStyle = FlatStyle.Flat;
            _urlDragButton.FlatAppearance.BorderSize = 0;
            _urlDragButton.AccessibleName = "Drag current URL to a bookmark bar";
            _backButton = CreateToolbarButton("‹", 40);
            _forwardButton = CreateToolbarButton("›", 40);
            _reloadButton = CreateToolbarButton("↻", 40);
            _downloadsButton = CreateToolbarButton("⇩", 40);
            _copyModeButton = CreateToolbarButton("⧉", 40);
            _cloneButton = CreateToolbarButton("⧈", 40);
            Button homeButton = CreateToolbarButton("⌂", 40);
            Button newButton = CreateToolbarButton("✚", 40);
            Button libraryButton = CreateToolbarButton("▦", 40);
            Button recentButton = CreateToolbarButton("◷", 40);
            _bookmarksButton = CreateToolbarButton("★", 40);
            _newBookmarkButton = CreateToolbarButton("☆", 40);
            _notesButton = CreateToolbarButton("✎", 40);
            _sleepButton = CreateToolbarButton("☾", 40);
            _caffeineButton = CreateToolbarButton("☕", 40);
            _trayShortcutButton = CreateToolbarButton("📌", 40);
            Button settingsButton = CreateToolbarButton("⚙", 40);
            Button captureButton = CreateToolbarButton("▣", 40);
            Button openExternalButton = CreateToolbarButton("↗", 40);
            _menuButton = CreateToolbarButton("⋯", 54);
            _menuButton.Tag = "⋯ More options";
            SetToolbarButtonGlyph(_menuButton, "⋯");
            _menuButton.Visible = false;

            _statusLabel = new Label();
            _statusLabel.AutoEllipsis = true;
            _statusLabel.TextAlign = ContentAlignment.MiddleRight;
            _statusLabel.ForeColor = Color.FromArgb(90, 90, 90);
            _statusLabel.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            _statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _statusLabel.Left = 0;
            _statusLabel.Top = 3;
            _statusLabel.Height = 31;
            _statusLabel.Width = 0;

            _addressBox = new TextBox();
            _addressBox.Height = 25;
            _addressBox.Top = 6;
            _addressBox.BorderStyle = BorderStyle.FixedSingle;
            _addressBox.Font = new Font("Segoe UI", 8.5f);
            _addressBox.Text = Record.Url ?? AppController.HomeUrl;
            _addressSuggestionTexts = new AutoCompleteStringCollection();
            _addressSuggestionUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // Keep the native edit control passive.  WinForms' AutoComplete window can
            // flash and steal focus when WebView2 reports redirects/title changes.  The
            // same local bookmark/recent index is still used by Enter resolution below.
            _addressBox.AutoCompleteMode = AutoCompleteMode.None;
            _addressBox.AutoCompleteSource = AutoCompleteSource.None;
            _addressBox.AutoCompleteCustomSource = _addressSuggestionTexts;

            _addressGoButton = CreateToolbarButton("↵", 30);
            _addressGoButton.Height = 28;
            _addressGoButton.Font = new Font("Segoe UI Symbol", 11f);
            _addressGoButton.AccessibleName = "Go to the typed address";

            AddToolbarAction(_appearanceButton, "✦ Title, ID and icon");
            AddToolbarAction(newButton, "✚ New window");
            AddToolbarAction(_backButton, "‹ Back");
            AddToolbarAction(_forwardButton, "› Forward");
            AddToolbarAction(_reloadButton, "↻ Reload");
            AddToolbarAction(_downloadsButton, "⇩ Downloads (Ctrl+J)");
            AddToolbarAction(_copyModeButton, "⧉ Copy mode: enable text selection");
            AddToolbarAction(_notesButton, "✎ Show or hide notes");
            AddToolbarAction(_caffeineButton, "☕ Keep this window awake");
            AddToolbarAction(_sleepButton, "☾ Hibernate this window");
            AddToolbarAction(settingsButton, "⚙ Options");
            AddToolbarSeparator();
            AddToolbarAction(_cloneButton, "⧈ Clone this window");
            AddToolbarAction(openExternalButton, "↗ Open this page in browser");
            AddToolbarSeparator();
            AddToolbarAction(libraryButton, "▦ Windows and recent");
            AddToolbarAction(_bookmarksButton, "★ Open bookmarks sidebar");
            AddToolbarAction(_newBookmarkButton, "☆ Add current page to bookmarks");
            AddToolbarAction(_trayShortcutButton, "📌 Keep this page as a tray shortcut");
            AddToolbarAction(recentButton, "◷ Recent conversations");
            AddToolbarSeparator();
            AddToolbarAction(captureButton, "▣ Capture full page as PNG");
            _toolbar.Controls.Add(_menuButton);
            _toolbar.Controls.Add(_quickIdentifierBox);
            _toolbar.Controls.Add(_quickColorButton);
            _toolbar.Controls.Add(_urlDragButton);
            _toolbar.Controls.Add(_addressBox);
            _toolbar.Controls.Add(_addressGoButton);
            _toolbar.Controls.Add(_statusLabel);

            _browserHost = new Panel();
            _browserHost.Dock = DockStyle.Fill;
            _browserHost.BackColor = Color.White;
            _browserHost.Resize += delegate { ResizeBrowserToHost(); };

            _webArea = new Panel();
            _webArea.Dock = DockStyle.Fill;
            _webArea.BackColor = Color.White;

            _sleepPanel = new Panel();
            _sleepPanel.Dock = DockStyle.Fill;
            _sleepPanel.BackColor = UiTheme.LightBackground;

            _sleepLayout = new TableLayoutPanel();
            _sleepLayout.Dock = DockStyle.Fill;
            _sleepLayout.ColumnCount = 1;
            _sleepLayout.RowCount = 4;
            _sleepLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _sleepLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 42f));
            _sleepLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _sleepLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _sleepLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 58f));

            _sleepTitle = new Label();
            _sleepTitle.AutoSize = true;
            _sleepTitle.Anchor = AnchorStyles.None;
            _sleepTitle.Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold);
            _sleepTitle.ForeColor = Color.FromArgb(45, 45, 45);
            _sleepTitle.Text = _lastTitle;
            _sleepTitle.MaximumSize = new Size(620, 0);

            _restoreRow = new FlowLayoutPanel();
            _restoreRow.AutoSize = true;
            _restoreRow.Anchor = AnchorStyles.None;
            _restoreRow.FlowDirection = FlowDirection.TopDown;
            _restoreRow.WrapContents = false;

            _sleepExplanation = new Label();
            _sleepExplanation.AutoSize = true;
            _sleepExplanation.MaximumSize = new Size(560, 0);
            _sleepExplanation.TextAlign = ContentAlignment.MiddleCenter;
            _sleepExplanation.ForeColor = Color.FromArgb(95, 95, 95);
            _sleepExplanation.Font = new Font("Segoe UI", 10f);
            _sleepExplanation.Text = "This window is hibernated and does not keep a browser in memory.\r\nYour session, cache and conversation remain saved.";

            _restoreButton = new Button();
            _restoreButton.AutoSize = true;
            _restoreButton.Padding = new Padding(16, 7, 16, 7);
            _restoreButton.Margin = new Padding(0, 16, 0, 0);
            _restoreButton.FlatStyle = FlatStyle.System;
            _restoreButton.Text = "Restore window";
            _restoreButton.Click += async delegate { await EnsureBrowserReadyAsync(true); };

            _restoreRow.Controls.Add(_sleepExplanation);
            _restoreRow.Controls.Add(_restoreButton);
            _sleepLayout.Controls.Add(new Panel(), 0, 0);
            _sleepLayout.Controls.Add(_sleepTitle, 0, 1);
            _sleepLayout.Controls.Add(_restoreRow, 0, 2);
            _sleepLayout.Controls.Add(new Panel(), 0, 3);
            _sleepPanel.Controls.Add(_sleepLayout);

            _sleepPreview = new PictureBox();
            _sleepPreview.Dock = DockStyle.Fill;
            // The snapshot is only a visual placeholder while the real WebView is released.
            // Stretch it with the host instead of preserving the captured viewport ratio;
            // otherwise a narrow/tall resized window shows large letterbox bands and looks
            // like a shrunken Full-HD page.
            // Preserve the viewport aspect ratio when the window is resized.
            // Stretching a page snapshot makes text and controls look deformed.
            _sleepPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _sleepPreview.BackColor = UiTheme.DarkBackground;
            _sleepPreview.Visible = false;
            _sleepPreview.Cursor = Cursors.Hand;
            _sleepPreview.Click += delegate { RestoreFromSleepArea(); };
            _sleepPreview.Paint += HibernationSurfacePaint;
            _sleepPanel.Click += delegate { RestoreFromSleepArea(); };
            _sleepPanel.Paint += HibernationSurfacePaint;
            _sleepLayout.Click += delegate { RestoreFromSleepArea(); };
            _sleepLayout.Paint += HibernationSurfacePaint;
            _sleepTitle.Click += delegate { RestoreFromSleepArea(); };
            _sleepExplanation.Click += delegate { RestoreFromSleepArea(); };
            _sleepPanel.Controls.Add(_sleepPreview);

            _startDashboardImages = new List<Image>();
            _startFaviconRequests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _startPanel = CreateStartDashboard(
                out _startFrequentTiles,
                out _startRecentTiles,
                out _startCountdownLabel);
            _startPanel.Visible = false;

            _bookmarksPanel = CreateBookmarksPanel(
                out _bookmarksTree,
                out _bookmarksHeader,
                out _bookmarksHeading,
                out _bookmarksCloseButton);
            _bookmarksPanel.Visible = Record.BookmarksVisible;
            RefreshBookmarks();

            _notesPanel = CreateNotesPanel(
                out _notesEditor,
                out _notesHeading,
                out _noteFormatBar,
                out _notesHeader,
                out _notesFooter,
                out _notesCloseButton);
            if (!_app.NoteDocumentExists(Record) && Record.NoteImages.Count > 0)
                MigrateLegacyNoteImages();
            _notesPanel.Visible = Record.NotesVisible;
            _webArea.Controls.Add(_browserHost);
            _webArea.Controls.Add(_sleepPanel);
            _webArea.Controls.Add(_startPanel);
            _webArea.Controls.Add(_bookmarksPanel);
            Controls.Add(_webArea);
            Controls.Add(_notesPanel);
            Controls.Add(_toolbar);

            _toolTip = new ToolTip();
            _toolTip.SetToolTip(_backButton, "Back (Alt+Left)");
            _toolTip.SetToolTip(_forwardButton, "Forward (Alt+Right)");
            _toolTip.SetToolTip(_reloadButton, "Reload (Ctrl+R)");
            _toolTip.SetToolTip(_downloadsButton, "Open downloads (Ctrl+J)");
            _toolTip.SetToolTip(_copyModeButton, "Copy mode: allow selecting and copying text on this page");
            _toolTip.SetToolTip(_cloneButton, "Clone this window with the same page, title, ID, icon, color and notes");
            _toolTip.SetToolTip(_appearanceButton, "Click to choose an icon. Right-click for title, ID and advanced options.");
            _toolTip.SetToolTip(_quickColorButton, "Choose this window color");
            _toolTip.SetToolTip(_urlDragButton, "Drag this URL to the bookmarks bar or a folder in Google Chrome");
            _toolTip.SetToolTip(homeButton, "Default home page (Ctrl+N)");
            _toolTip.SetToolTip(newButton, "New window (Ctrl+Shift+N)");
            _toolTip.SetToolTip(_notesButton, "Show or hide attached notes (Ctrl+Shift+M)");
            _toolTip.SetToolTip(_newBookmarkButton, "Add the current page to bookmarks");
            _toolTip.SetToolTip(_trayShortcutButton, "Keep or remove this page as an icon beside the clock");
            _toolTip.SetToolTip(recentButton, "Recent conversations saved locally");
            _toolTip.SetToolTip(_sleepButton, "Hibernate this window (Ctrl+H)");
            _toolTip.SetToolTip(_caffeineButton, "Keep this window awake: it will not auto-suspend or hibernate");
            _toolTip.SetToolTip(libraryButton, "Window and recent manager (Ctrl+Shift+L)");
            _toolTip.SetToolTip(settingsButton, "Options");
            _toolTip.SetToolTip(captureButton, "Capture the full page as a PNG file");
            _toolTip.SetToolTip(_quickIdentifierBox, "Type 1 to 50 and press Enter (for example, 7 becomes TD-D07)");
            _toolTip.SetToolTip(_addressBox, "Type a bookmark, recent page, URL or Google search, then press Enter");
            _toolTip.SetToolTip(_addressGoButton, "Open a matching bookmark/recent page, URL or Google search");
            _toolTip.SetToolTip(_startSearchBox, "Buscar un bookmark por nombre o URL; Enter abre el primero");

            _backButton.Click += delegate { if (CoreWebView2 != null && CoreWebView2.CanGoBack) CoreWebView2.GoBack(); };
            _forwardButton.Click += delegate { if (CoreWebView2 != null && CoreWebView2.CanGoForward) CoreWebView2.GoForward(); };
            _reloadButton.Click += delegate { if (CoreWebView2 != null) CoreWebView2.Reload(); else BeginRestore(); };
            _downloadsButton.Click += async delegate { await OpenDownloadsAsync(); };
            _copyModeButton.Click += async delegate { await ToggleCopyModeAsync(); };
            _cloneButton.Click += delegate { _app.CloneWindow(this, true); };
            _appearanceButton.Click += delegate { ShowCustomization(); };
            _appearanceButton.MouseUp += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button == MouseButtons.Right)
                    ShowQuickIconPalette(_appearanceButton);
            };
            _quickColorButton.Click += delegate { ShowQuickColorPalette(_quickColorButton); };
            _urlDragButton.MouseDown += UrlDragButtonMouseDown;
            homeButton.Click += delegate { Navigate(AppController.HomeUrl); };
            newButton.Click += delegate { _app.OpenWindow(AppController.HomeUrl, true); };
            libraryButton.Click += delegate { _app.ShowLibrary(); };
            _notesButton.Click += delegate { ToggleNotes(); };
            _bookmarksButton.Click += delegate { ToggleBookmarks(); };
            _newBookmarkButton.Click += delegate { AddCurrentPageBookmark(); };
            _trayShortcutButton.Click += delegate { _app.ToggleTrayShortcut(Record); UpdateTrayShortcutVisual(); };
            recentButton.Click += delegate { ShowRecentsMenu(recentButton); };
            _sleepButton.Click += delegate { Hibernate(); };
            _caffeineButton.Click += delegate { ToggleCaffeine(); };
            settingsButton.Click += delegate { _app.ShowSettings(); };
            captureButton.Click += async delegate { await CaptureFullPageAsync(); };
            openExternalButton.Click += delegate { TryOpenExternal(Record.Url); };
            _menuButton.Click += delegate { ShowOverflowMenu(_menuButton); };
            _addressGoButton.Click += delegate { NavigateFromAddressBox(); };
            _quickIdentifierBox.KeyDown += QuickIdentifierKeyDown;
            _quickIdentifierBox.Leave += delegate { ApplyQuickIdentifier(); };
            _addressBox.Leave += delegate { RefreshAddressSuggestions(); };
            _addressBox.Enter += delegate
            {
                if (!_startDashboardVisible)
                    return;
                StopStartDashboardCountdown();
                if (!_startAddressEditPrepared)
                {
                    _addressBox.Clear();
                    _startAddressEditPrepared = true;
                }
            };
            _addressBox.MouseDown += delegate
            {
                if (_startDashboardVisible)
                    StopStartDashboardCountdown();
            };
            _addressBox.KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.KeyCode == Keys.Enter)
                {
                    NavigateFromAddressBox();
                    args.SuppressKeyPress = true;
                    args.Handled = true;
                }
            };

            if (Record.IsQuickConsult)
                ConfigureQuickConsultChrome();

            _startDashboardTimer = new System.Windows.Forms.Timer();
            _startDashboardTimer.Interval = 1000;
            _startDashboardTimer.Tick += StartDashboardTimerTick;

            _resourceTimer = new System.Windows.Forms.Timer();
            _resourceTimer.Interval = 3000;
            _resourceTimer.Tick += ResourceTimerTick;
            if (!_isHibernated)
                _resourceTimer.Start();

            _caffeinePulseTimer = new System.Windows.Forms.Timer();
            _caffeinePulseTimer.Interval = 2000;
            _caffeinePulseTimer.Tick += delegate
            {
                if (Record.CaffeineMode && !IsDisposed)
                {
                    _caffeinePulsePhase = !_caffeinePulsePhase;
                    UpdateCaffeineVisual();
                }
            };
            if (Record.CaffeineMode)
                _caffeinePulseTimer.Start();

            _hibernatePulseTimer = new System.Windows.Forms.Timer();
            _hibernatePulseTimer.Interval = 1200;
            _hibernatePulseTimer.Tick += delegate
            {
                if (_isHibernated && !IsDisposed)
                {
                    _hibernatePulsePhase = !_hibernatePulsePhase;
                    InvalidateHibernationBorder();
                }
            };
            if (_isHibernated)
                _hibernatePulseTimer.Start();

            Activated += WindowActivated;
            Deactivate += delegate { _inactiveSinceUtc = DateTime.UtcNow; };
            Resize += delegate
            {
                LayoutToolbar();
                ResizeBrowserToHost();
                if (WindowState == FormWindowState.Minimized)
                    _inactiveSinceUtc = DateTime.UtcNow.AddSeconds(-Math.Max(0, _app.State.Settings.SuspendAfterSeconds));
            };
            Shown += async delegate
            {
                LayoutToolbar();
                UpdateResourceUi();
                if (_startDashboardVisible)
                {
                    BeginStartDashboardCountdown();
                    return;
                }
                if (!_isHibernated)
                {
                    try { await EnsureBrowserReadyAsync(true); }
                    catch { }
                }
            };
            FormClosing += WindowFormClosing;
            FormClosed += delegate
            {
                _resourceTimer.Stop();
                _resourceTimer.Dispose();
                _caffeinePulseTimer.Stop();
                _caffeinePulseTimer.Dispose();
                _hibernatePulseTimer.Stop();
                _hibernatePulseTimer.Dispose();
                _startDashboardTimer.Stop();
                _startDashboardTimer.Dispose();
                foreach (Image image in _startDashboardImages)
                    image.Dispose();
                _startDashboardImages.Clear();
                _toolTip.Dispose();
                if (_sleepPreview.Image != null)
                    _sleepPreview.Image.Dispose();
                if (_presentationIcon != null)
                    _presentationIcon.Dispose();
                _app.OnWindowClosed(this, _closingForExit);
            };

            ApplyPresentation();
            LoadHibernatePreview();
            UpdateResourceUi();
        }

        public async Task EnsureBrowserReadyAsync(bool navigate)
        {
            if (_resourceTimer != null)
                _resourceTimer.Start();
            if (_webView != null && _webView.CoreWebView2 != null)
            {
                Resume();
                return;
            }

            if (_initializationTask == null)
                _initializationTask = InitializeBrowserAsync(navigate);
            await _initializationTask;
        }

        public void ActivateAndRestore()
        {
            if (!Visible)
                Show();
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            if (!Record.IsQuickConsult)
                ShowInTaskbar = true;
            Activate();
            BringToFront();
            if (_startDashboardVisible)
            {
                _startPanel.BringToFront();
                return;
            }
            if (!_isHibernated || !Record.KeepHibernated)
                BeginRestore();
        }

        public void Navigate(string url)
        {
            Record.Url = NormalizeNavigationUrl(url);
            if (_addressBox != null && !_addressBox.Focused)
                _addressBox.Text = Record.Url;
            if (_startDashboardVisible)
            {
                StartBrowserFromDashboard(Record.Url);
                return;
            }
            if (_isHibernated || CoreWebView2 == null)
            {
                Record.KeepHibernated = false;
                BeginRestore();
                return;
            }
            Resume();
            CoreWebView2.Navigate(Record.Url);
        }

        private void NavigateFromAddressBox()
        {
            string requested = _addressBox == null ? string.Empty : _addressBox.Text;
            string url = ResolveAddressInput(requested);
            if (_isHibernated)
            {
                Record.Url = url;
                if (_addressBox != null)
                    _addressBox.Text = url;
                Record.KeepHibernated = false;
                BeginRestore();
                return;
            }
            _addressBox.Text = url;
            Navigate(url);
        }

        private string ResolveAddressInput(string requested)
        {
            string text = (requested ?? string.Empty).Trim();
            if (text.Length == 0)
                return AppController.HomeUrl;

            string selectedUrl;
            if (_addressSuggestionUrls.TryGetValue(text, out selectedUrl) && IsWebAddress(selectedUrl))
                return selectedUrl;

            string directUrl;
            if (TryGetDirectWebAddress(text, out directUrl))
                return directUrl;

            string savedPage = FindSavedPageForAddress(text);
            if (!string.IsNullOrWhiteSpace(savedPage))
                return savedPage;

            return "https://www.google.cl/search?q=" + Uri.EscapeDataString(text) + "&hl=es";
        }

        private string FindSavedPageForAddress(string query)
        {
            BookmarkRecord bookmark = _app.State.Bookmarks
                .Where(delegate(BookmarkRecord item)
                {
                    return item != null && !string.IsNullOrWhiteSpace(item.Url) &&
                        AddressMatchScore((item.Title ?? string.Empty) + " " + (item.FolderPath ?? string.Empty), item.Url, query) > 0;
                })
                .OrderByDescending(delegate(BookmarkRecord item)
                {
                    return AddressMatchScore((item.Title ?? string.Empty) + " " + (item.FolderPath ?? string.Empty), item.Url, query);
                })
                .ThenBy(delegate(BookmarkRecord item) { return item.Title ?? item.Url; })
                .FirstOrDefault();
            if (bookmark != null)
                return bookmark.Url;

            RecentRecord recent = _app.State.Recents
                .Where(delegate(RecentRecord item)
                {
                    return item != null && !string.IsNullOrWhiteSpace(item.Url) &&
                        AddressMatchScore(item.Title, item.Url, query) > 0;
                })
                .OrderByDescending(delegate(RecentRecord item)
                {
                    return AddressMatchScore(item.Title, item.Url, query);
                })
                .ThenByDescending(delegate(RecentRecord item) { return item.LastOpenedUtc; })
                .FirstOrDefault();
            return recent == null ? string.Empty : recent.Url;
        }

        private static int AddressMatchScore(string title, string url, string query)
        {
            string needle = NormalizeStartSearchText(query);
            if (needle.Length == 0)
                return 0;
            string searchableTitle = NormalizeStartSearchText(title);
            string searchableUrl = NormalizeStartSearchText(url);
            if (string.Equals(searchableTitle, needle, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(searchableUrl, needle, StringComparison.OrdinalIgnoreCase))
                return 4;
            if (searchableTitle.StartsWith(needle, StringComparison.OrdinalIgnoreCase) ||
                searchableUrl.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
                return 3;
            if (searchableTitle.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;
            return searchableUrl.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0;
        }

        private static bool TryGetDirectWebAddress(string text, out string url)
        {
            url = string.Empty;
            Uri uri;
            if (Uri.TryCreate(text, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "about"))
            {
                url = uri.AbsoluteUri;
                return true;
            }

            if (text.IndexOfAny(new char[] { ' ', '\t', '\r', '\n' }) >= 0 ||
                text.IndexOf("://", StringComparison.Ordinal) >= 0)
                return false;

            string host = text.Split(new char[] { '/', '?', '#' }, 2)[0];
            System.Net.IPAddress ipAddress;
            bool looksLikeHost = host.IndexOf('.') >= 0 ||
                host.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
                System.Net.IPAddress.TryParse(host.Split(':')[0], out ipAddress);
            if (!looksLikeHost || !Uri.TryCreate("https://" + text, UriKind.Absolute, out uri))
                return false;
            url = uri.AbsoluteUri;
            return true;
        }

        private static bool IsWebAddress(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "about");
        }

        private void QuickIdentifierKeyDown(object sender, KeyEventArgs args)
        {
            if (args.KeyCode != Keys.Enter)
                return;
            ApplyQuickIdentifier();
            _addressBox.Focus();
            args.SuppressKeyPress = true;
            args.Handled = true;
        }

        private void ApplyQuickIdentifier()
        {
            if (_quickIdentifierBox == null)
                return;
            string identifier = _app.NormalizeIdentifierInput(_quickIdentifierBox.Text);
            if (string.Equals(identifier, Record.Identifier ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                _quickIdentifierBox.Text = CompactIdentifierText(identifier);
                return;
            }
            Record.Identifier = identifier;
            _quickIdentifierBox.Text = CompactIdentifierText(identifier);
            _app.RememberIdentifier(identifier);
            ApplyPresentation();
            _app.WindowPresentationChanged();
        }

        private static string CompactIdentifierText(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return string.Empty;
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
                identifier.Trim(),
                "^TD-D0*(?<number>[0-9]{1,2})$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
                return identifier.Trim();
            int number;
            return int.TryParse(match.Groups["number"].Value, out number) ? number.ToString() : identifier.Trim();
        }

        public async void Suspend()
        {
            if (Record.CaffeineMode || _isHibernated || _isSuspended || _suspendAttemptInProgress || CoreWebView2 == null)
                return;
            CoreWebView2 core = CoreWebView2;
            if (core == null)
                return;
            _suspendAttemptInProgress = true;
            try
            {
                bool suspended = await core.TrySuspendAsync();
                if (_isHibernating || _isHibernated || _webView == null)
                    return;
                _isSuspended = suspended;
            }
            catch (Exception ex)
            {
                // Hibernation can dispose WebView2 while TrySuspendAsync is completing.
                // That transition is expected and should not generate a log storm.
                if (!_isHibernating && !_isHibernated && !IsDisposed && !Disposing &&
                    _webView != null && !_webView.IsDisposed && !(ex is InvalidOperationException))
                    _app.Log("No se pudo suspender una ventana", ex);
            }
            finally
            {
                _suspendAttemptInProgress = false;
                UpdateResourceUi();
            }
        }

        public void Resume()
        {
            if (!_isSuspended || CoreWebView2 == null)
                return;
            try
            {
                CoreWebView2.Resume();
                _isSuspended = false;
            }
            catch (Exception ex)
            {
                _app.Log("No se pudo reanudar una ventana", ex);
            }
            UpdateResourceUi();
        }

        public async void Hibernate()
        {
            try
            {
                await HibernateAsync();
            }
            catch (Exception ex)
            {
                _app.Log("No se pudo hibernar una ventana", ex);
            }
        }

        public async Task HibernateAsync()
        {
            if (_isHibernated || _isHibernating)
                return;

            // A manual hibernation is an explicit override of the keep-awake choice.
            Record.CaffeineMode = false;
            _isHibernating = true;
            _resourceTimer.Stop();
            UpdateRecordFromWindow();
            try
            {
                await CaptureHibernatePreviewAsync();
                if (_webView != null)
                {
                    try
                    {
                        _browserHost.Controls.Remove(_webView);
                        _webView.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _app.Log("No se pudo liberar completamente una ventana", ex);
                    }
                    _webView = null;
                }
                _initializationTask = null;
                _isHibernated = true;
                _isSuspended = false;
                _resourceTimer.Stop();
                Record.Hibernated = true;
                _sleepTitle.Text = DisplayTitle;
                LoadHibernatePreview();
                ApplySleepTheme();
                _sleepPanel.Visible = true;
                _sleepPanel.BringToFront();
                UpdateResourceUi();
                _app.QueueSave();
            }
            finally
            {
                _isHibernating = false;
            }
        }

        public void ToggleCaffeine()
        {
            Record.CaffeineMode = !Record.CaffeineMode;
            if (Record.CaffeineMode)
            {
                Record.KeepHibernated = false;
                if (_isHibernated)
                    BeginRestore();
                else
                    Resume();
            }
            UpdateResourceUi();
            _app.WindowPresentationChanged();
        }

        public void TogglePermanentHibernate()
        {
            Record.KeepHibernated = !Record.KeepHibernated;
            if (Record.KeepHibernated)
            {
                Record.CaffeineMode = false;
                if (!_isHibernated)
                    Hibernate();
            }
            UpdateResourceUi();
            _app.WindowPresentationChanged();
        }

        private async Task CaptureHibernatePreviewAsync()
        {
            if (!_app.State.Settings.CapturePreviewOnHibernate || CoreWebView2 == null)
                return;

            string target = _app.GetHibernationPreviewPath(Record);
            string temporary = target + ".new";
            try
            {
                // Hibernation previews capture only the current WebView viewport.
                // The manual Capture action remains responsible for full-page images.
#if false
                try
                {
                    // Capture the complete document before releasing WebView2. This keeps the
                    // hibernated surface useful even when the visible area was scrolled.
                    string result = await CoreWebView2.CallDevToolsProtocolMethodAsync(
                        "Page.captureScreenshot",
                        "{\"format\":\"png\",\"captureBeyondViewport\":true}");
                    System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
                        result, "\\\"data\\\"\\s*:\\s*\\\"(?<data>[^\\\"]+)\\\"");
                    if (match.Success)
                    {
                        screenshot = Convert.FromBase64String(match.Groups["data"].Value);
                        // Keep automatic hibernation bounded on very long pages. A manual
                        // full-page capture remains available from the Capture action.
                        if (screenshot.Length > 24L * 1024L * 1024L)
                            screenshot = null;
                    }
                }
                catch (Exception ex)
                {
                    _app.Log("No se pudo capturar la vista completa; se usará la vista visible", ex);
                }

                if (screenshot != null && screenshot.Length > 0)
                    File.WriteAllBytes(temporary, screenshot);
                else
                {
                    using (FileStream output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                        await CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, output);
                }
#endif
                using (FileStream output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                    await CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, output);
                using (Image source = Image.FromFile(temporary))
                using (Image preview = CreateHibernatePreview(source))
                    preview.Save(target, ImageFormat.Jpeg);
                File.Delete(temporary);
                Record.HibernationPreviewUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _app.Log("No se pudo guardar la vista previa de hibernación", ex);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    try { File.Delete(temporary); }
                    catch { }
                }
            }
        }

        private static Image CreateHibernatePreview(Image source)
        {
            const int maximumWidth = 1280;
            const int maximumHeight = 900;
            float scale = Math.Min(1f, Math.Min(
                (float)maximumWidth / Math.Max(1, source.Width),
                (float)maximumHeight / Math.Max(1, source.Height)));
            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));
            Bitmap preview = new Bitmap(width, height);
            using (Graphics graphics = Graphics.FromImage(preview))
            {
                graphics.Clear(Color.White);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, width, height));
            }
            return preview;
        }

        private void LoadHibernatePreview()
        {
            try
            {
                string path = _app.GetHibernationPreviewPath(Record);
                if (!File.Exists(path))
                {
                    _sleepPreview.Visible = false;
                    _sleepLayout.Visible = true;
                    return;
                }

                using (Image source = Image.FromFile(path))
                {
                    Image previous = _sleepPreview.Image;
                    _sleepPreview.Image = new Bitmap(source);
                    if (previous != null)
                        previous.Dispose();
                }
                _sleepLayout.Visible = false;
                _sleepPreview.Visible = true;
                _sleepPreview.BringToFront();
            }
            catch (Exception ex)
            {
                _app.Log("No se pudo mostrar la vista previa de hibernación", ex);
                _sleepPreview.Visible = false;
                _sleepLayout.Visible = true;
            }
        }

        private async Task CaptureFullPageAsync()
        {
            if (CoreWebView2 == null || _isHibernated)
            {
                MessageBox.Show("Restore this window before capturing the page.", AppController.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string path = _app.CreateCapturePath(Record);
            try
            {
                _statusLabel.Text = "Capturing full page…";
                string result = await CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Page.captureScreenshot",
                    "{\"format\":\"png\",\"captureBeyondViewport\":true}");
                System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(result, "\\\"data\\\"\\s*:\\s*\\\"(?<data>[^\\\"]+)\\\"");
                if (!match.Success)
                    throw new InvalidDataException("The browser did not return an image.");
                File.WriteAllBytes(path, Convert.FromBase64String(match.Groups["data"].Value));
                _statusLabel.Text = "Full-page capture saved";
                Process.Start(path);
            }
            catch (Exception ex)
            {
                _app.Log("No se pudo capturar una página completa", ex);
                MessageBox.Show("The full-page capture could not be completed.\r\n\r\n" + ex.Message, AppController.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowQuickIconPalette(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            const int columns = 7;
            const int cellSize = 32;
            int rows = (int)Math.Ceiling(IconVisuals.GlyphChoices.Length / (double)columns);
            Panel palette = new Panel();
            palette.Size = new Size(columns * cellSize + 8, rows * cellSize + 8);
            palette.BackColor = _app.State.Settings.DarkMode ? UiTheme.DarkSurface : Color.White;

            for (int index = 0; index < IconVisuals.GlyphChoices.Length; index++)
            {
                string glyph = IconVisuals.GlyphChoices[index];
                Button choice = new Button();
                choice.Text = glyph;
                choice.Font = new Font("Segoe UI Symbol", 11f);
                choice.FlatStyle = FlatStyle.Flat;
                choice.FlatAppearance.BorderColor = _app.State.Settings.DarkMode ? Color.FromArgb(75, 87, 102) : Color.FromArgb(210, 210, 210);
                choice.BackColor = _app.State.Settings.DarkMode ? UiTheme.DarkInput : Color.White;
                choice.ForeColor = _app.State.Settings.DarkMode ? UiTheme.DarkText : Color.FromArgb(35, 35, 35);
                choice.Size = new Size(28, 28);
                choice.Location = new Point(4 + (index % columns) * cellSize, 4 + (index / columns) * cellSize);
                choice.Cursor = Cursors.Hand;
                string chosen = glyph;
                choice.Click += delegate
                {
                    Record.IconGlyph = IconVisuals.GetGlyphFromText(chosen);
                    ApplyPresentation();
                    _app.RememberDomainProfile(Record);
                    _app.WindowPresentationChanged();
                    menu.Close();
                };
                palette.Controls.Add(choice);
            }

            ToolStripControlHost host = new ToolStripControlHost(palette);
            host.AutoSize = false;
            host.Margin = Padding.Empty;
            host.Padding = Padding.Empty;
            host.Size = palette.Size;
            menu.Items.Add(host);
            menu.Items.Add("✦ More title, ID and icon options…", null, delegate
            {
                menu.Close();
                ShowCustomization();
            });
            menu.Show(owner, new Point(0, owner.Height));
        }

        private void ShowQuickColorPalette(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            const int columns = 6;
            const int cellSize = 30;
            int rows = (int)Math.Ceiling(QuickColors.Length / (double)columns);
            Panel palette = new Panel();
            palette.Size = new Size(columns * cellSize + 8, rows * cellSize + 8);
            palette.BackColor = _app.State.Settings.DarkMode ? UiTheme.DarkSurface : Color.White;

            for (int index = 0; index < QuickColors.Length; index++)
            {
                Color color = QuickColors[index];
                Button choice = new Button();
                choice.FlatStyle = FlatStyle.Flat;
                choice.FlatAppearance.BorderColor = Color.FromArgb(245, 245, 245);
                choice.BackColor = color;
                choice.Size = new Size(26, 26);
                choice.Location = new Point(4 + (index % columns) * cellSize, 4 + (index / columns) * cellSize);
                choice.Cursor = Cursors.Hand;
                Color chosen = color;
                choice.Click += delegate
                {
                    ApplyQuickColor(chosen);
                    menu.Close();
                };
                palette.Controls.Add(choice);
            }

            ToolStripControlHost host = new ToolStripControlHost(palette);
            host.AutoSize = false;
            host.Margin = Padding.Empty;
            host.Padding = Padding.Empty;
            host.Size = palette.Size;
            menu.Items.Add(host);
            menu.Items.Add("🎨 More colors…", null, delegate
            {
                menu.Close();
                using (ColorDialog dialog = new ColorDialog())
                {
                    dialog.Color = IconVisuals.GetColor(Record);
                    dialog.FullOpen = true;
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        ApplyQuickColor(dialog.Color);
                }
            });
            menu.Show(owner, new Point(0, owner.Height));
        }

        private void ApplyQuickColor(Color color)
        {
            Record.IconColorArgb = color.ToArgb();
            ApplyPresentation();
            _app.RememberDomainProfile(Record);
            _app.WindowPresentationChanged();
        }

        private void UrlDragButtonMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _isHibernated)
                return;
            string url = Record.Url ?? string.Empty;
            Uri parsed;
            if (!Uri.TryCreate(url, UriKind.Absolute, out parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                return;

            DataObject data = new DataObject();
            string bookmarkTitle = DisplayTitle;
            if (string.IsNullOrWhiteSpace(bookmarkTitle) || string.Equals(bookmarkTitle, "Browser window", StringComparison.OrdinalIgnoreCase))
                bookmarkTitle = parsed.Host;
            string encodedUrl = WebUtility.HtmlEncode(parsed.AbsoluteUri);
            string encodedTitle = WebUtility.HtmlEncode(bookmarkTitle);
            string htmlAnchor = "<a href=\"" + encodedUrl + "\">" + encodedTitle + "</a>";
            data.SetData(DataFormats.UnicodeText, true, parsed.AbsoluteUri);
            data.SetData(DataFormats.Text, true, parsed.AbsoluteUri);
            data.SetData(DataFormats.Html, false, htmlAnchor);
            data.SetData("text/uri-list", false, parsed.AbsoluteUri + "\r\n");
            data.SetData("UniformResourceLocator", false, parsed.AbsoluteUri);
            data.SetData("UniformResourceLocatorW", false, Encoding.Unicode.GetBytes(parsed.AbsoluteUri + "\0"));
            DoDragDrop(data, DragDropEffects.Link | DragDropEffects.Copy);
        }

        public void ShowCustomization()
        {
            using (WindowCustomizationForm form = new WindowCustomizationForm(_app, this))
                form.ShowDialog(this);
        }

        private void RestoreFromSleepArea()
        {
            if (!_isHibernated)
                return;
            Record.KeepHibernated = false;
            BeginRestore();
        }

        public void ToggleNotes()
        {
            SetNotesVisible(!Record.NotesVisible);
        }

        public void ShowNotes()
        {
            SetNotesVisible(true);
        }

        public void ToggleBookmarks()
        {
            SetBookmarksVisible(!Record.BookmarksVisible);
        }

        private void AddCurrentPageBookmark()
        {
            string url = Record.Url;
            if (CoreWebView2 != null && !string.IsNullOrWhiteSpace(CoreWebView2.Source))
                url = CoreWebView2.Source;
            try
            {
                _app.AddBookmark(url, DisplayTitle);
                _statusLabel.Text = "★ Bookmark saved";
                if (!Record.BookmarksVisible)
                    SetBookmarksVisible(true);
                else
                    RefreshBookmarks();
            }
            catch (Exception ex)
            {
                _app.Log("No se pudo guardar el bookmark actual", ex);
                MessageBox.Show(ex.Message, AppController.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void RefreshBookmarks()
        {
            RefreshAddressSuggestions();
            if (_bookmarksTree == null || _bookmarksTree.IsDisposed)
                return;

            _bookmarksTree.BeginUpdate();
            try
            {
                _bookmarksTree.Nodes.Clear();
                Dictionary<string, TreeNode> folders = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
                foreach (BookmarkRecord bookmark in _app.State.Bookmarks
                    .Where(delegate(BookmarkRecord item) { return item != null && !string.IsNullOrWhiteSpace(item.Url); })
                    .OrderBy(delegate(BookmarkRecord item) { return item.FolderPath ?? string.Empty; })
                    .ThenBy(delegate(BookmarkRecord item) { return item.Title ?? item.Url; }))
                {
                    TreeNodeCollection collection = _bookmarksTree.Nodes;
                    string path = bookmark.FolderPath ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        string current = string.Empty;
                        foreach (string part in path.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            current = current.Length == 0 ? part : current + "\\" + part;
                            TreeNode folder;
                            if (!folders.TryGetValue(current, out folder))
                            {
                                folder = new TreeNode("📁 " + part);
                                folder.Name = current;
                                collection.Add(folder);
                                folders[current] = folder;
                            }
                            collection = folder.Nodes;
                        }
                    }
                    string title = string.IsNullOrWhiteSpace(bookmark.Title) ? bookmark.Url : bookmark.Title;
                    TreeNode item = new TreeNode("🔖 " + title);
                    item.Tag = bookmark;
                    item.ToolTipText = bookmark.Url;
                    collection.Add(item);
                }
                if (_bookmarksTree.Nodes.Count == 0)
                {
                    TreeNode empty = new TreeNode("No bookmarks yet");
                    empty.ForeColor = _app.State.Settings.DarkMode ? UiTheme.DarkMutedText : Color.FromArgb(110, 110, 110);
                    _bookmarksTree.Nodes.Add(empty);
                }
                else
                    _bookmarksTree.ExpandAll();
            }
            finally
            {
                _bookmarksTree.EndUpdate();
            }
        }

        private void RefreshAddressSuggestions()
        {
            if (_addressSuggestionTexts == null || _addressSuggestionUrls == null)
                return;
            if (_addressBox != null && _addressBox.Focused)
                return;

            _addressSuggestionTexts.Clear();
            _addressSuggestionUrls.Clear();

            // Keep the address bar's local index deliberately bounded.  It contains
            // bookmarks first, then the newest history, and never touches the network.
            foreach (BookmarkRecord bookmark in _app.State.Bookmarks
                .Where(delegate(BookmarkRecord item) { return item != null && !string.IsNullOrWhiteSpace(item.Url); })
                .OrderBy(delegate(BookmarkRecord item) { return item.Title ?? item.Url; })
                .Take(120))
            {
                string folder = string.IsNullOrWhiteSpace(bookmark.FolderPath) ? string.Empty : " - " + bookmark.FolderPath;
                AddAddressSuggestion((string.IsNullOrWhiteSpace(bookmark.Title) ? bookmark.Url : bookmark.Title) + folder, bookmark.Url);
                AddAddressSuggestion(bookmark.Url, bookmark.Url);
            }

            foreach (RecentRecord recent in _app.State.Recents
                .Where(delegate(RecentRecord item) { return item != null && !string.IsNullOrWhiteSpace(item.Url); })
                .OrderByDescending(delegate(RecentRecord item) { return item.LastOpenedUtc; })
                .Take(60))
            {
                AddAddressSuggestion(string.IsNullOrWhiteSpace(recent.Title) ? recent.Url : recent.Title, recent.Url);
                AddAddressSuggestion(recent.Url, recent.Url);
            }
        }

        private void AddAddressSuggestion(string text, string url)
        {
            string label = (text ?? string.Empty).Trim();
            if (label.Length == 0 || string.IsNullOrWhiteSpace(url) || _addressSuggestionUrls.ContainsKey(label))
                return;
            _addressSuggestionUrls[label] = url;
            _addressSuggestionTexts.Add(label);
        }

        private void SetBookmarksVisible(bool visible)
        {
            Record.BookmarksVisible = visible;
            _bookmarksPanel.Visible = visible;
            if (visible)
                RefreshBookmarks();
            _app.WindowPresentationChanged();
        }

        public void ApplyPresentation()
        {
            if (IsDisposed || Disposing)
                return;
            Icon previousIcon = _presentationIcon;
            _presentationIcon = IconVisuals.CreateWindowIcon(Record);
            Icon = _presentationIcon;
            if (previousIcon != null)
                previousIcon.Dispose();

            Text = WindowTitle();
            _sleepTitle.Text = DisplayTitle;
            _notesHeading.Text = NotesHeadingText();
            SetToolbarButtonGlyph(_appearanceButton, IconVisuals.GetGlyph(Record));
            if (!_quickIdentifierBox.Focused)
                _quickIdentifierBox.Text = CompactIdentifierText(Record.Identifier);
            Color accent = Record.IsQuickConsult
                ? Color.FromArgb(75, 78, 125)
                : IconVisuals.GetColor(Record);
            Color foreground = IconVisuals.ContrastColor(accent);
            bool dark = _app.State.Settings.DarkMode;
            Color nativeBackground = dark ? UiTheme.DarkBackground : UiTheme.LightBackground;
            Color nativeSurface = dark ? UiTheme.DarkSurface : Color.White;
            Color noteSurface = dark ? IconVisuals.DarkTint(accent) : IconVisuals.WaterTint(accent);
            Color noteText = dark ? UiTheme.DarkText : Color.FromArgb(35, 55, 55);
            BackColor = nativeBackground;
            UiTheme.ApplyWindowChrome(this, dark);
            _webArea.BackColor = nativeBackground;
            _browserHost.BackColor = nativeSurface;
            ApplySleepTheme();
            _toolbar.BackColor = accent;
            _statusLabel.BackColor = accent;
            _statusLabel.ForeColor = foreground;
            foreach (Button button in _toolbarButtons)
            {
                button.BackColor = accent;
                button.ForeColor = foreground;
                button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent);
                button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(accent);
            }
            foreach (KeyValuePair<Button, string> glyph in _toolbarGlyphs.ToArray())
                SetToolbarButtonGlyph(glyph.Key, glyph.Value);
            foreach (Panel separator in _toolbarSeparators)
                separator.BackColor = dark ? Color.FromArgb(196, 210, 226) : Color.FromArgb(46, 63, 82);
            _menuButton.BackColor = accent;
            _menuButton.ForeColor = foreground;
            SetToolbarButtonGlyph(_menuButton, _toolbarGlyphs.ContainsKey(_menuButton) ? _toolbarGlyphs[_menuButton] : "⋯");
            _menuButton.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent);
            _menuButton.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(accent);
            _quickColorButton.BackColor = dark ? UiTheme.DarkInput : Color.White;
            _quickColorButton.ForeColor = accent;
            _quickColorButton.FlatAppearance.BorderColor = dark ? Color.FromArgb(95, 109, 125) : ControlPaint.Dark(accent);
            _quickColorButton.FlatAppearance.MouseOverBackColor = dark ? ControlPaint.Light(UiTheme.DarkInput) : IconVisuals.PaleColor(accent);
            _urlDragButton.BackColor = accent;
            _urlDragButton.ForeColor = foreground;
            _urlDragButton.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent);
            _notesPanel.BackColor = noteSurface;
            _bookmarksPanel.BackColor = dark ? UiTheme.DarkSurface : Color.White;
            _bookmarksHeader.BackColor = accent;
            _bookmarksHeading.ForeColor = foreground;
            _bookmarksCloseButton.BackColor = accent;
            _bookmarksCloseButton.ForeColor = foreground;
            _bookmarksCloseButton.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent);
            _bookmarksTree.BackColor = dark ? UiTheme.DarkInput : Color.White;
            _bookmarksTree.ForeColor = dark ? UiTheme.DarkText : Color.FromArgb(40, 40, 40);
            _notesHeader.BackColor = accent;
            _notesHeading.ForeColor = foreground;
            _notesFooter.BackColor = noteSurface;
            _notesFooter.ForeColor = dark ? UiTheme.DarkMutedText : Color.FromArgb(55, 80, 80);
            _notesCloseButton.BackColor = accent;
            _notesCloseButton.ForeColor = foreground;
            _notesCloseButton.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent);
            _notesEditor.BackColor = noteSurface;
            _notesEditor.ForeColor = noteText;
            _noteFormatBar.BackColor = noteSurface;
            foreach (Button button in _noteFormatBar.Controls.OfType<Button>())
            {
                button.BackColor = dark ? UiTheme.DarkInput : IconVisuals.PaleColor(accent);
                button.ForeColor = noteText;
                button.FlatAppearance.BorderColor = accent;
                button.FlatAppearance.MouseOverBackColor = dark ? ControlPaint.Light(UiTheme.DarkInput) : ControlPaint.Light(IconVisuals.PaleColor(accent));
            }
            _addressBox.BackColor = dark ? UiTheme.DarkInput : Color.White;
            _addressBox.ForeColor = dark ? UiTheme.DarkText : Color.FromArgb(35, 35, 35);
            _addressGoButton.BackColor = accent;
            _addressGoButton.ForeColor = foreground;
            _addressGoButton.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent);
            _addressGoButton.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(accent);
            SetNotesVisible(Record.NotesVisible, false);
            ApplyStartDashboardTheme();
            UpdateCaffeineVisual();
            UpdateCopyModeVisual();
            UpdateTrayShortcutVisual();
            UpdateHibernationPulse();
            LayoutToolbar();
        }

        private void ApplySleepTheme()
        {
            bool dark = _app.State.Settings.DarkMode;
            UiTheme.ApplyNativeTree(_sleepPanel, dark);
            Color background = dark ? UiTheme.DarkBackground : UiTheme.LightBackground;
            Color surface = dark ? UiTheme.DarkSurface : UiTheme.LightBackground;
            _sleepPanel.BackColor = background;
            _sleepLayout.BackColor = background;
            _restoreRow.BackColor = background;
            _sleepTitle.BackColor = background;
            _sleepTitle.ForeColor = dark ? UiTheme.DarkText : Color.FromArgb(45, 45, 45);
            _sleepExplanation.BackColor = background;
            _sleepExplanation.ForeColor = dark ? UiTheme.DarkMutedText : Color.FromArgb(95, 95, 95);
            _sleepPreview.BackColor = background;
            _restoreButton.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.System;
            _restoreButton.BackColor = dark ? surface : SystemColors.Control;
            _restoreButton.ForeColor = dark ? UiTheme.DarkText : UiTheme.LightText;
            _restoreButton.FlatAppearance.BorderColor = dark ? Color.FromArgb(82, 96, 112) : SystemColors.ControlDark;
            _restoreButton.FlatAppearance.MouseOverBackColor = dark ? ControlPaint.Light(surface) : SystemColors.ControlLight;
        }

        public void RefreshResourceTimer()
        {
            _inactiveSinceUtc = DateTime.UtcNow;
            if (_resourceTimer != null)
            {
                if (_isHibernated)
                    _resourceTimer.Stop();
                else
                    _resourceTimer.Start();
            }
            UpdateResourceUi();
        }

        public void UpdateRecordFromWindow()
        {
            if (CoreWebView2 != null && CoreWebView2.Source != null)
                Record.Url = CoreWebView2.Source;
            Record.Title = _lastTitle;
            if (_notesEditor != null && _notesEditor.Text != Record.NoteText)
                Record.NoteText = _notesEditor.Text;
            if (_noteDocumentDirty)
            {
                if (_app.SaveNoteDocument(Record, _notesEditor))
                    _noteDocumentDirty = false;
            }
            Record.Hibernated = _isHibernated;
            Record.LastActiveUtc = Record.LastActiveUtc == DateTime.MinValue ? DateTime.UtcNow : Record.LastActiveUtc;

            Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            if (bounds.Width >= MinimumSize.Width && bounds.Height >= MinimumSize.Height)
            {
                Record.X = bounds.X;
                Record.Y = bounds.Y;
                Record.Width = bounds.Width;
                Record.Height = bounds.Height;
            }
            Record.Maximized = WindowState == FormWindowState.Maximized;
        }

        public void CloseForApplicationExit()
        {
            _closingForExit = true;
            Close();
        }

        public void CloseForRecycleBin()
        {
            _closingForExit = true;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Shift | Keys.N))
            {
                _app.OpenWindow(AppController.HomeUrl, true);
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.L))
            {
                _app.ShowLibrary();
                return true;
            }
            if (keyData == (Keys.Control | Keys.H))
            {
                Hibernate();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.M))
            {
                ToggleNotes();
                return true;
            }
            if (keyData == (Keys.Control | Keys.N))
            {
                Navigate(AppController.HomeUrl);
                return true;
            }
            if (keyData == (Keys.Control | Keys.R) || keyData == Keys.F5)
            {
                if (CoreWebView2 != null)
                    CoreWebView2.Reload();
                else
                    BeginRestore();
                return true;
            }
            if (keyData == (Keys.Control | Keys.J))
            {
                BeginInvoke((MethodInvoker)async delegate { await OpenDownloadsAsync(); });
                return true;
            }
            if (keyData == (Keys.Alt | Keys.Left) && CoreWebView2 != null && CoreWebView2.CanGoBack)
            {
                CoreWebView2.GoBack();
                return true;
            }
            if (keyData == (Keys.Alt | Keys.Right) && CoreWebView2 != null && CoreWebView2.CanGoForward)
            {
                CoreWebView2.GoForward();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private async Task OpenDownloadsAsync()
        {
            try
            {
                await EnsureBrowserReadyAsync(true);
                if (CoreWebView2 != null)
                    CoreWebView2.OpenDefaultDownloadDialog();
            }
            catch (Exception ex)
            {
                _app.Log("No se pudo abrir la ventana de descargas", ex);
            }
        }

        private async Task InitializeBrowserAsync(bool navigate)
        {
            _sleepPanel.Visible = true;
            _statusLabel.Text = "Starting…";
            try
            {
                CoreWebView2Environment environment = await _app.GetEnvironmentAsync();
                if (IsDisposed)
                    return;

                WebView2 browser = new WebView2();
                browser.Dock = DockStyle.None;
                browser.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                browser.DefaultBackgroundColor = Color.White;
                _browserHost.Controls.Add(browser);
                browser.BringToFront();
                _webView = browser;
                ResizeBrowserToHost();

                await browser.EnsureCoreWebView2Async(environment);
                ResizeBrowserToHost();
                await ConfigureBrowserAsync(browser.CoreWebView2);

                _isHibernated = false;
                _isSuspended = false;
                Record.Hibernated = false;
                _sleepPanel.Visible = false;
                _browserHost.BringToFront();
                UpdateResourceUi();

                if (navigate && !string.Equals(Record.Url, "about:blank", StringComparison.OrdinalIgnoreCase))
                    browser.CoreWebView2.Navigate(string.IsNullOrWhiteSpace(Record.Url) ? AppController.HomeUrl : Record.Url);
            }
            catch (Exception ex)
            {
                _app.Log("No se pudo iniciar WebView2", ex);
                _isHibernated = true;
                Record.Hibernated = true;
                _sleepTitle.Text = "This page could not be opened";
                _statusLabel.Text = "WebView2 error";
                _sleepPanel.Visible = true;
                _sleepPanel.BringToFront();
                MessageBox.Show(
                    "No se pudo iniciar el componente web de Windows.\r\n\r\n" + ex.Message +
                    "\r\n\r\nInstala o repara Microsoft Edge WebView2 Runtime y vuelve a intentarlo.",
                    AppController.ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                throw;
            }
            finally
            {
                _initializationTask = null;
            }
        }

        private void ResizeBrowserToHost()
        {
            if (_browserHost == null || _browserHost.IsDisposed || _webView == null || _webView.IsDisposed)
                return;

            Rectangle bounds = _browserHost.ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            // WebView2 owns a native child window. Explicit bounds keep that native
            // surface synchronized when the parent is resized or moved between
            // virtual desktops; Dock alone can retain the previous viewport size.
            _webView.SetBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height, BoundsSpecified.All);
        }

        private async Task ConfigureBrowserAsync(CoreWebView2 core)
        {
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.IsBuiltInErrorPageEnabled = true;
            // WebView2 does not show the browser permission bubble by itself.
            // Grant notification permission for the current site so services such
            // as WhatsApp, Gmail and ChatGPT can display desktop notifications.
            core.PermissionRequested += delegate(object sender, CoreWebView2PermissionRequestedEventArgs args)
            {
                if (args.PermissionKind == CoreWebView2PermissionKind.Notifications)
                    args.State = CoreWebView2PermissionState.Allow;
            };
            core.WebMessageReceived += delegate(object sender, CoreWebView2WebMessageReceivedEventArgs args)
            {
                string message = args.TryGetWebMessageAsString();
                if (string.Equals(message, "rpm-browser-user-activity", StringComparison.Ordinal))
                    MarkUserActivity();
            };
            await core.AddScriptToExecuteOnDocumentCreatedAsync(
                "(function(){var send=function(){try{chrome.webview.postMessage('rpm-browser-user-activity');}catch(e){}};" +
                "['pointerdown','keydown','wheel','touchstart','input','focus'].forEach(function(name){" +
                "window.addEventListener(name,send,{capture:true,passive:true});});})();");
            await core.AddScriptToExecuteOnDocumentCreatedAsync(
                "(function(){if(window.__rpmCopyHookInstalled)return;window.__rpmCopyHookInstalled=true;" +
                "window.__rpmCopyMode=false;window.__rpmSetCopyMode=function(enabled){window.__rpmCopyMode=!!enabled;" +
                "var id='rpm-browser-copy-mode-style',style=document.getElementById(id);if(window.__rpmCopyMode){" +
                "if(!style){style=document.createElement('style');style.id=id;(document.head||document.documentElement).appendChild(style);}" +
                "style.textContent='html,body,body *{user-select:text !important;-webkit-user-select:text !important;-webkit-touch-callout:default !important;}';" +
                "}else if(style&&style.parentNode){style.parentNode.removeChild(style);}};" +
                "var allow=function(e){if(window.__rpmCopyMode)e.stopImmediatePropagation();};" +
                "document.addEventListener('selectstart',allow,true);document.addEventListener('copy',allow,true);" +
                "document.addEventListener('cut',allow,true);document.addEventListener('contextmenu',allow,true);})();");

            core.FaviconChanged += async delegate
            {
                await CaptureFaviconAsync(core);
            };

            core.NavigationStarting += delegate(object sender, CoreWebView2NavigationStartingEventArgs args)
            {
                MarkUserActivity();
                Uri uri;
                if (Uri.TryCreate(args.Uri, UriKind.Absolute, out uri) &&
                    uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != "about")
                {
                    args.Cancel = true;
                    TryOpenExternal(args.Uri);
                    return;
                }
                _statusLabel.Text = "Loading…";
            };
            core.NavigationCompleted += async delegate(object sender, CoreWebView2NavigationCompletedEventArgs args)
            {
                UpdateNavigationButtons();
                if (!args.IsSuccess)
                    _statusLabel.Text = "Could not load · click ↻ to retry";
                else
                {
                    UpdateResourceUi();
                    ScheduleQuickInitialRecovery(core);
                    await CaptureFaviconAsync(core);
                }
                if (_copyModeEnabled)
                    await ApplyCopyModeAsync();
                SaveCurrentPage();
                if (_addressBox == null || !_addressBox.Focused)
                    RefreshAddressSuggestions();
            };
            core.SourceChanged += delegate
            {
                if (!string.IsNullOrWhiteSpace(core.Source))
                    Record.Url = core.Source;
                if (_addressBox != null && !_addressBox.Focused &&
                    !string.Equals(_addressBox.Text, Record.Url, StringComparison.Ordinal))
                    _addressBox.Text = Record.Url;
                _app.QueueSave();
            };
            core.DocumentTitleChanged += delegate
            {
                if (!string.IsNullOrWhiteSpace(core.DocumentTitle))
                {
                    _lastTitle = core.DocumentTitle;
                    Record.Title = _lastTitle;
                    Text = WindowTitle();
                    _sleepTitle.Text = DisplayTitle;
                }
                SaveCurrentPage();
            };
            core.HistoryChanged += delegate { UpdateNavigationButtons(); };
            core.WindowCloseRequested += delegate { Close(); };
            core.NewWindowRequested += NewWindowRequested;
            core.ProcessFailed += delegate(object sender, CoreWebView2ProcessFailedEventArgs args)
            {
                _statusLabel.Text = "Web view stopped · restoring";
                BeginInvoke((MethodInvoker)delegate
                {
                    Hibernate();
                    BeginRestore();
                });
            };
        }

        private async Task CaptureFaviconAsync(CoreWebView2 core)
        {
            if (core == null || core != CoreWebView2 || Record.IsQuickConsult || Record.UseFavicon != true || _faviconCaptureInProgress)
                return;

            _faviconCaptureInProgress = true;
            try
            {
                string path = _app.GetFaviconPath(Record);
                bool stored = false;
                try
                {
                    using (Stream source = await core.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png))
                        stored = TryStoreFavicon(source, path);
                }
                catch
                {
                    // Some sites expose the favicon URI before WebView2 can decode it.
                    // The direct request below is the light-weight fallback for that case.
                }

                if (!stored)
                    stored = await TryDownloadFaviconAsync(core.FaviconUri, path);
                if (!stored)
                    stored = await TryDownloadFaviconAsync(BuildDefaultFaviconUri(core.Source ?? Record.Url), path);
                if (!stored)
                    stored = await TryDownloadFaviconAsync(BuildFaviconServiceUri(core.Source ?? Record.Url), path);

                if (stored)
                {
                    Record.FaviconPath = path;
                    StoreSharedSiteFavicon(core.Source ?? Record.Url, path);
                    if (!IsDisposed && !Disposing)
                    {
                        ApplyPresentation();
                        _app.WindowPresentationChanged();
                    }
                }
            }
            catch (Exception ex)
            {
                _app.Log("No se pudo extraer el favicon", ex);
            }
            finally
            {
                _faviconCaptureInProgress = false;
            }
        }

        private void StoreSharedSiteFavicon(string pageUrl, string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return;
            try
            {
                string cachePath = _app.GetSiteFaviconPath(pageUrl);
                if (!string.IsNullOrWhiteSpace(cachePath))
                    File.Copy(sourcePath, cachePath, true);
            }
            catch
            {
                // The individual window icon remains available if shared caching fails.
            }
        }

        private static bool TryStoreFavicon(Stream source, string path)
        {
            if (source == null || string.IsNullOrWhiteSpace(path))
                return false;
            try
            {
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (Image image = Image.FromStream(source))
                    using (Bitmap bitmap = new Bitmap(image))
                        bitmap.Save(temporary, ImageFormat.Png);
                    File.Copy(temporary, path, true);
                    return File.Exists(path) && new FileInfo(path).Length > 0;
                }
                finally
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> TryDownloadFaviconAsync(string faviconUri, string targetPath)
        {
            Uri uri;
            if (!Uri.TryCreate(faviconUri, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return false;

            return await Task.Run(delegate
            {
                try
                {
                    HttpWebRequest request = WebRequest.Create(uri) as HttpWebRequest;
                    if (request == null)
                        return false;
                    request.Method = "GET";
                    request.UserAgent = "RPMBrowserLite favicon cache";
                    request.Accept = "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8";
                    request.AllowAutoRedirect = true;
                    request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    request.Timeout = 4500;
                    request.ReadWriteTimeout = 4500;
                    using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                    {
                        if (response == null || (int)response.StatusCode >= 400)
                            return false;
                        using (Stream source = response.GetResponseStream())
                            return TryStoreFavicon(source, targetPath);
                    }
                }
                catch
                {
                    return false;
                }
            });
        }

        private static string BuildDefaultFaviconUri(string pageUrl)
        {
            Uri page;
            if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out page) ||
                (page.Scheme != Uri.UriSchemeHttp && page.Scheme != Uri.UriSchemeHttps))
                return string.Empty;
            return page.Scheme + "://" + page.Authority + "/favicon.ico";
        }

        private static string BuildFaviconServiceUri(string pageUrl)
        {
            Uri page;
            if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out page) ||
                (page.Scheme != Uri.UriSchemeHttp && page.Scheme != Uri.UriSchemeHttps))
                return string.Empty;
            string origin = page.Scheme + "://" + page.Authority;
            return "https://www.google.com/s2/favicons?sz=64&domain_url=" + Uri.EscapeDataString(origin);
        }

        private void ScheduleQuickInitialRecovery(CoreWebView2 core)
        {
            if (!Record.IsQuickConsult || _quickInitialRecoveryScheduled || core == null)
                return;

            string source = core.Source ?? string.Empty;
            if (source.IndexOf("gemini.google.com", StringComparison.OrdinalIgnoreCase) < 0)
                return;

            _quickInitialRecoveryScheduled = true;
            BeginInvoke((MethodInvoker)async delegate
            {
                try
                {
                    // Gemini is a client-rendered application. WebView2 can report the first
                    // navigation as complete while its toolbar/input modules are still hydrating.
                    await Task.Delay(1600);
                    if (!IsDisposed && core == CoreWebView2)
                        core.Reload();
                }
                catch (Exception ex)
                {
                    _app.Log("No se pudo completar la recuperación inicial de Quick Gemini", ex);
                }
            });
        }

        private async void NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            Uri uri;
            if (Uri.TryCreate(args.Uri, UriKind.Absolute, out uri) && ShouldOpenExternally(uri))
            {
                args.Handled = true;
                TryOpenExternal(args.Uri);
                return;
            }

            CoreWebView2Deferral deferral = args.GetDeferral();
            try
            {
                BrowserWindow child = await _app.OpenPopupWindowAsync();
                args.NewWindow = child.CoreWebView2;
                args.Handled = true;
                child.ActivateAndRestore();
            }
            catch (Exception ex)
            {
                _app.Log("No se pudo crear una ventana emergente", ex);
                args.Handled = false;
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void WindowActivated(object sender, EventArgs e)
        {
            MarkUserActivity();
            if (_isHibernated)
            {
                if (!Record.KeepHibernated && _app.State.Settings.RestoreHibernatedOnlyOnClick != true)
                    BeginRestore();
            }
            else
                Resume();
        }

        private void MarkUserActivity()
        {
            DateTime now = DateTime.UtcNow;
            Record.LastActiveUtc = now;
            _inactiveSinceUtc = now;
            if (!Record.IsQuickConsult)
                _app.QueueSave();
        }

        private void ConfigureQuickConsultChrome()
        {
            // Keep temporary consultations deliberately separate from the full browser interface.
            _toolbar.Height = 38;
            // The temporary window does not need a second title such as
            // "Refresh Quick Gemini · temporary consultation" in its native chrome.
            // Keep the compact toolbar as the only visible header.
            Text = string.Empty;
            _reloadButton.Tag = string.Empty;
            SetToolbarButtonGlyph(_reloadButton, "↻");
            _reloadButton.Height = 30;
            _reloadButton.Width = 40;
            _toolTip.SetToolTip(_reloadButton, "Refresh Gemini");
            _addressBox.Visible = false;
            _addressGoButton.Visible = false;
            _menuButton.Visible = false;
        }

        private void WindowFormClosing(object sender, FormClosingEventArgs e)
        {
            UpdateRecordFromWindow();
            if (e.CloseReason != CloseReason.UserClosing)
            {
                _closingForExit = true;
                _app.SaveNow();
            }
        }

        private void ResourceTimerTick(object sender, EventArgs e)
        {
            TimeSpan inactive = DateTime.UtcNow - _inactiveSinceUtc;
            if (Record.IsQuickConsult)
            {
                int closeAfterMinutes = _app.State.Settings.QuickConsultCloseAfterMinutes;
                if (closeAfterMinutes > 0 && inactive.TotalMinutes >= closeAfterMinutes)
                    Close();
                return;
            }
            if (Record.CaffeineMode)
            {
                Resume();
                return;
            }
            if (_isHibernated || _isHibernating || ContainsFocus || Form.ActiveForm == this)
                return;

            int hibernateMinutes = _app.State.Settings.HibernateAfterMinutes;
            int suspendSeconds = _app.State.Settings.SuspendAfterSeconds;

            if (hibernateMinutes > 0 && inactive.TotalMinutes >= hibernateMinutes)
            {
                Hibernate();
                return;
            }
            if (suspendSeconds > 0 && inactive.TotalSeconds >= suspendSeconds)
                Suspend();
        }

        private void BeginRestore()
        {
            if (_startDashboardVisible)
            {
                StartBrowserFromDashboard(Record.Url);
                return;
            }
            BeginInvoke((MethodInvoker)async delegate
            {
                try { await EnsureBrowserReadyAsync(true); }
                catch { }
            });
        }

        private void SaveCurrentPage()
        {
            if (CoreWebView2 == null || string.IsNullOrWhiteSpace(CoreWebView2.Source))
                return;
            Record.Url = CoreWebView2.Source;
            _app.TrackRecent(Record.Url, _lastTitle);
        }

        private void UpdateNavigationButtons()
        {
            _backButton.Enabled = CoreWebView2 != null && CoreWebView2.CanGoBack;
            _forwardButton.Enabled = CoreWebView2 != null && CoreWebView2.CanGoForward;
        }

        private void UpdateResourceUi()
        {
            if (IsDisposed || Disposing)
                return;
            if (Record.IsQuickConsult)
            {
                _statusLabel.Text = string.Empty;
                _sleepButton.Enabled = false;
                _caffeineButton.Enabled = false;
                LayoutToolbar();
                return;
            }
            if (_isHibernated)
            {
                _statusLabel.Text = Record.KeepHibernated
                    ? "● Locked hibernated · snapshot on disk · no active browser"
                    : "● Hibernated · snapshot on disk · no active browser";
                _sleepButton.Enabled = false;
                _caffeineButton.Enabled = true;
                _addressGoButton.Text = "Restore";
                _addressGoButton.Width = 66;
            }
            else if (_isSuspended)
            {
                _statusLabel.Text = "● Sleeping · CPU paused";
                _sleepButton.Enabled = true;
                _caffeineButton.Enabled = true;
                _addressGoButton.Text = "↵";
                _addressGoButton.Width = 30;
            }
            else
            {
                _statusLabel.Text = Record.CaffeineMode ? "● Caffeine · kept awake" : "● Active";
                _sleepButton.Enabled = true;
                _caffeineButton.Enabled = true;
                _addressGoButton.Text = "↵";
                _addressGoButton.Width = 30;
            }
            UpdateCaffeineVisual();
            UpdateHibernationPulse();
            LayoutToolbar();
        }

        private void UpdateHibernationPulse()
        {
            if (_hibernatePulseTimer == null)
                return;
            int style = HibernationIndicatorStyle;
            if (_isHibernated && style > 0)
            {
                int interval = style == 2 ? 3000 : (style == 4 ? 2400 : (style == 3 ? 1800 : 1500));
                _hibernatePulseTimer.Interval = interval;
                if (style < 5 && !_hibernatePulseTimer.Enabled)
                    _hibernatePulseTimer.Start();
                if (style == 5)
                    _hibernatePulseTimer.Stop();
                InvalidateHibernationBorder();
            }
            else
            {
                _hibernatePulseTimer.Stop();
                _hibernatePulsePhase = false;
                InvalidateHibernationBorder();
            }
        }

        private void InvalidateHibernationBorder()
        {
            if (_sleepPanel != null && !_sleepPanel.IsDisposed)
                _sleepPanel.Invalidate();
            if (_sleepPreview != null && !_sleepPreview.IsDisposed)
                _sleepPreview.Invalidate();
            if (_sleepLayout != null && !_sleepLayout.IsDisposed)
                _sleepLayout.Invalidate();
        }

        private void HibernationSurfacePaint(object sender, PaintEventArgs e)
        {
            if (!_isHibernated || HibernationIndicatorStyle == 0)
                return;
            Control surface = sender as Control;
            if (surface == null || surface.ClientSize.Width < 4 || surface.ClientSize.Height < 4)
                return;
            int style = HibernationIndicatorStyle;
            Color bright;
            Color dim;
            float thickness;
            switch (style)
            {
                case 2:
                    bright = Color.FromArgb(142, 42, 89, 150);
                    dim = Color.FromArgb(52, 22, 48, 86);
                    thickness = 1.5f;
                    break;
                case 3:
                    bright = Color.FromArgb(170, 44, 172, 198);
                    dim = Color.FromArgb(56, 20, 74, 92);
                    thickness = 1.5f;
                    break;
                case 4:
                    bright = Color.FromArgb(160, 137, 86, 202);
                    dim = Color.FromArgb(52, 65, 37, 106);
                    thickness = 1.5f;
                    break;
                case 5:
                    bright = Color.FromArgb(120, 47, 94, 157);
                    dim = bright;
                    thickness = 1f;
                    break;
                default:
                    bright = Color.FromArgb(170, 54, 128, 211);
                    dim = Color.FromArgb(58, 28, 64, 112);
                    thickness = 1.5f;
                    break;
            }
            Color line = _hibernatePulsePhase ? bright : dim;
            using (Pen pen = new Pen(line, thickness))
            {
                Rectangle frame = new Rectangle(1, 1, surface.ClientSize.Width - 3, surface.ClientSize.Height - 3);
                e.Graphics.DrawRectangle(pen, frame);
            }
        }

        private int HibernationIndicatorStyle
        {
            get { return Math.Max(0, Math.Min(5, _app.State.Settings.HibernationIndicatorStyle)); }
        }

        private void UpdateCaffeineVisual()
        {
            if (_caffeineButton == null)
                return;
            if (Record.CaffeineMode)
            {
                if (_caffeinePulseTimer != null && !_caffeinePulseTimer.Enabled)
                    _caffeinePulseTimer.Start();
                Color green = _caffeinePulsePhase
                    ? Color.FromArgb(74, 184, 94)
                    : Color.FromArgb(47, 151, 73);
                _caffeineButton.BackColor = green;
                _caffeineButton.ForeColor = Color.White;
                _caffeineButton.FlatAppearance.BorderSize = 1;
                _caffeineButton.FlatAppearance.BorderColor = Color.FromArgb(156, 239, 165);
                _caffeineButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(95, 201, 112);
            }
            else
            {
                if (_caffeinePulseTimer != null)
                    _caffeinePulseTimer.Stop();
                Color accent = IconVisuals.GetColor(Record);
                _caffeineButton.BackColor = accent;
                _caffeineButton.ForeColor = IconVisuals.ContrastColor(accent);
                _caffeineButton.FlatAppearance.BorderSize = 0;
                _caffeineButton.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent);
            }
        }

        private void UpdateTrayShortcutVisual()
        {
            if (_trayShortcutButton == null)
                return;
            if (Record.TrayShortcut)
            {
                _trayShortcutButton.BackColor = Color.FromArgb(53, 145, 87);
                _trayShortcutButton.ForeColor = Color.White;
                _trayShortcutButton.FlatAppearance.BorderSize = 1;
                _trayShortcutButton.FlatAppearance.BorderColor = Color.FromArgb(164, 235, 175);
            }
            else
            {
                Color accent = Record.IsQuickConsult ? Color.FromArgb(75, 78, 125) : IconVisuals.GetColor(Record);
                _trayShortcutButton.BackColor = accent;
                _trayShortcutButton.ForeColor = IconVisuals.ContrastColor(accent);
                _trayShortcutButton.FlatAppearance.BorderSize = 0;
            }
        }

        private async Task ToggleCopyModeAsync()
        {
            _copyModeEnabled = !_copyModeEnabled;
            UpdateCopyModeVisual();
            await ApplyCopyModeAsync();
        }

        private async Task ApplyCopyModeAsync()
        {
            CoreWebView2 core = CoreWebView2;
            if (core == null || IsDisposed || Disposing)
                return;

            try
            {
                await core.ExecuteScriptAsync(
                    "if(window.__rpmSetCopyMode){window.__rpmSetCopyMode(" +
                    (_copyModeEnabled ? "true" : "false") + ");}");
            }
            catch (Exception ex)
            {
                // A navigation can replace the document between the button click and this call.
                _app.Log("No se pudo aplicar el modo de copia", ex);
            }
        }

        private void UpdateCopyModeVisual()
        {
            if (_copyModeButton == null)
                return;

            if (_copyModeEnabled)
            {
                SetToolbarButtonGlyph(_copyModeButton, "✓");
                _copyModeButton.BackColor = Color.FromArgb(42, 137, 177);
                _copyModeButton.ForeColor = Color.White;
                _copyModeButton.FlatAppearance.BorderSize = 1;
                _copyModeButton.FlatAppearance.BorderColor = Color.FromArgb(158, 225, 242);
                _copyModeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 158, 198);
            }
            else
            {
                Color accent = Record.IsQuickConsult ? Color.FromArgb(75, 78, 125) : IconVisuals.GetColor(Record);
                SetToolbarButtonGlyph(_copyModeButton, "⧉");
                _copyModeButton.BackColor = accent;
                _copyModeButton.ForeColor = IconVisuals.ContrastColor(accent);
                _copyModeButton.FlatAppearance.BorderSize = 0;
                _copyModeButton.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent);
            }
        }

        private string WindowTitle()
        {
            if (Record.IsQuickConsult)
                return string.Empty;
            string caption = DisplayTitle + " · " + AppController.ProductName;
            if (!string.IsNullOrWhiteSpace(Record.Identifier))
                caption += " · " + Record.Identifier.Trim();
            return caption;
        }

        private void ShowRecentsMenu(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            RecentRecord[] recents = _app.State.Recents
                .OrderByDescending(delegate(RecentRecord item) { return item.LastOpenedUtc; })
                .Take(12)
                .ToArray();
            if (recents.Length == 0)
            {
                ToolStripItem empty = menu.Items.Add("◷ No recent conversations yet");
                empty.Enabled = false;
            }
            foreach (RecentRecord recent in recents)
            {
                RecentRecord captured = recent;
                string label = captured.Title;
                if (label.Length > 54)
                    label = label.Substring(0, 51) + "…";
                menu.Items.Add("◷ " + label, null, delegate { Navigate(captured.Url); });
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("▦ View all…", null, delegate { _app.ShowLibrary(); });
            menu.Show(owner, new Point(0, owner.Height));
        }

        private void ShowMainMenu(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 10.5f);
            menu.ImageScalingSize = new Size(24, 24);
            menu.Items.Add("Icon: automatic favicon or ICO/PNG...", null, delegate { ShowCustomization(); });
            menu.Items.Add(new ToolStripSeparator());
            AddQuickConsultMenuItems(menu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("✚ New window", null, delegate { _app.OpenWindow(AppController.HomeUrl, true); });
            menu.Items.Add("▦ Windows and recent", null, delegate { _app.ShowLibrary(); });
            menu.Items.Add("✦ Title, ID and icon…", null, delegate { ShowCustomization(); });
            menu.Items.Add("✎ Show attached notes", null, delegate { ShowNotes(); });
            menu.Items.Add(Record.CaffeineMode ? "☕ Turn caffeine off" : "☕ Keep this window awake", null, delegate { ToggleCaffeine(); });
            menu.Items.Add("☾ Hibernate this window", null, delegate { Hibernate(); });
            menu.Items.Add(Record.KeepHibernated ? "⊘ Allow restore on activation" : "⊘ Keep permanently hibernated", null, delegate { TogglePermanentHibernate(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("⚙ Options", null, delegate { _app.ShowSettings(); });
            menu.Items.Add("↗ Open this page in browser", null, delegate { TryOpenExternal(Record.Url); });
            menu.Items.Add("ⓘ About " + AppController.ProductName + " " + AppController.ProductVersion, null, delegate { _app.ShowAbout(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("× Exit completely", null, delegate { _app.ExitApplication(); });
            menu.Show(owner, new Point(owner.Width - menu.PreferredSize.Width, owner.Height));
        }

        private void ShowOverflowMenu(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 10.5f);
            menu.ImageScalingSize = new Size(24, 24);
            menu.Items.Add("Icon: automatic favicon or ICO/PNG...", null, delegate { ShowCustomization(); });
            menu.Items.Add(new ToolStripSeparator());
            foreach (Button button in _toolbarButtons)
            {
                if (button.Visible)
                    continue;

                Button captured = button;
                string label = captured.Tag as string;
                ToolStripItem item = menu.Items.Add(string.IsNullOrWhiteSpace(label) ? captured.Text : label);
                item.Enabled = captured.Enabled;
                item.Click += delegate { ExecuteToolbarAction(captured); };
            }

            if (menu.Items.Count > 0)
                menu.Items.Add(new ToolStripSeparator());
            AddQuickConsultMenuItems(menu);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Record.KeepHibernated ? "⊘ Allow restore on activation" : "⊘ Keep permanently hibernated", null, delegate { TogglePermanentHibernate(); });
            menu.Items.Add("ⓘ About " + AppController.ProductName + " " + AppController.ProductVersion, null, delegate { _app.ShowAbout(); });

            if (menu.Items.Count == 0)
            {
                ToolStripItem empty = menu.Items.Add("✓ All controls are already visible");
                empty.Enabled = false;
            }

            menu.Show(owner, new Point(Math.Max(0, owner.Width - menu.PreferredSize.Width), owner.Height));
        }

        private void AddQuickConsultMenuItems(ContextMenuStrip menu)
        {
            if (menu == null)
                return;
            menu.Items.Add("✦ Quick Gemini · invisible", null, delegate { _app.OpenQuickConsult(); });
            menu.Items.Add("◈ Quick ChatGPT · private", null, delegate { _app.OpenQuickChatGpt(); });
            menu.Items.Add("◉ Quick Duck.ai · private", null, delegate { _app.OpenQuickDuckAi(); });
        }

        private void ExecuteToolbarAction(Button button)
        {
            if (button == null || !button.Enabled)
                return;

            bool wasVisible = button.Visible;
            if (!wasVisible)
                button.Visible = true;
            button.PerformClick();
            if (!IsDisposed)
                LayoutToolbar();
        }

        private void AddToolbarAction(Button button, string accessibleLabel)
        {
            button.Tag = accessibleLabel;
            button.AccessibleName = accessibleLabel;
            // Keep the icon and its small caption on one line of the toolbar.
            // The old fixed width made labels such as "+ Bookmark" wrap or get
            // clipped when the window was narrow.  Measure the caption first and
            // let LayoutToolbar decide which complete actions fit or move to More.
            string caption = GetToolbarCaption(accessibleLabel);
            int measuredWidth = TextRenderer.MeasureText(caption, button.Font).Width + 12;
            // Keep a comfortable touch/keyboard target while allowing the overflow
            // menu to take over complete actions on narrow windows.
            button.Width = Math.Max(button.Width, Math.Max(44, Math.Min(104, measuredWidth)));
            button.AutoEllipsis = true;
            button.UseCompatibleTextRendering = false;
            SetToolbarButtonGlyph(button, button.Text);
            _toolbarButtons.Add(button);
            _toolbarItems.Add(button);
            _toolbar.Controls.Add(button);
        }

        private void AddToolbarSeparator()
        {
            Panel separator = new Panel();
            separator.Width = 1;
            separator.Height = 42;
            separator.Margin = new Padding(4, 5, 4, 0);
            separator.Tag = "Toolbar separator";
            _toolbarSeparators.Add(separator);
            _toolbarItems.Add(separator);
            _toolbar.Controls.Add(separator);
        }

        private void LayoutToolbar()
        {
            if (_toolbar == null || _toolbar.IsDisposed || _toolbar.ClientSize.Width <= 0)
                return;

            if (Record.IsQuickConsult)
            {
                foreach (Control item in _toolbarItems)
                    item.Visible = false;
                _quickIdentifierBox.Visible = false;
                _quickColorButton.Visible = false;
                _urlDragButton.Visible = false;
                _menuButton.Visible = false;
                _addressBox.Visible = false;
                _addressGoButton.Visible = false;
                _reloadButton.Visible = true;
                _reloadButton.Left = 8;
                _reloadButton.Top = 3;
                _statusLabel.Visible = false;
                _statusLabel.Text = string.Empty;
                return;
            }

            int availableWidth = _toolbar.ClientSize.Width;
            int allToolbarWidth = 4;
            foreach (Control item in _toolbarItems)
                allToolbarWidth += item is Panel ? item.Width + 9 : item.Width + 2;
            allToolbarWidth += _quickIdentifierBox.Width + _quickColorButton.Width + 8;

            int minimumAddressWidth = _isHibernated ? 220 : 180;
            int dragHandleWidth = _isHibernated ? 0 : _urlDragButton.Width + 4;
            int trailingWidth = _addressGoButton.Width + dragHandleWidth + 8;
            bool needsOverflow = allToolbarWidth + minimumAddressWidth + trailingWidth > availableWidth;
            int actionsLimit = availableWidth - minimumAddressWidth - trailingWidth -
                (needsOverflow ? _menuButton.Width + 4 : 0);
            int left = 4;
            _quickIdentifierBox.Visible = false;
            _quickColorButton.Visible = false;

            bool previousActionVisible = false;
            foreach (Control item in _toolbarItems)
            {
                Panel separator = item as Panel;
                if (separator != null)
                {
                    bool showSeparator = previousActionVisible && left + separator.Width <= actionsLimit;
                    separator.Visible = showSeparator;
                    if (showSeparator)
                    {
                        separator.Left = left + 4;
                        separator.Top = 5;
                        left += separator.Width + 9;
                    }
                    continue;
                }

                Button button = item as Button;
                bool isAppearance = button == _appearanceButton;
                bool visible = left + button.Width + (isAppearance ? _quickIdentifierBox.Width + _quickColorButton.Width + 8 : 0) <= actionsLimit;
                button.Visible = visible;
                if (visible)
                {
                    previousActionVisible = true;
                    button.Left = left;
                    button.Top = 2;
                    left += button.Width + 2;
                    if (isAppearance)
                    {
                        _quickIdentifierBox.Visible = true;
                        _quickIdentifierBox.Left = left;
                        _quickIdentifierBox.Top = 13;
                        left += _quickIdentifierBox.Width + 4;
                        _quickColorButton.Visible = true;
                        _quickColorButton.Left = left;
                        _quickColorButton.Top = 12;
                        left += _quickColorButton.Width + 4;
                    }
                }
            }

            _menuButton.Visible = needsOverflow;
            if (needsOverflow)
            {
                _menuButton.Left = availableWidth - _menuButton.Width - 4;
                _menuButton.Top = 2;
            }

            int addressRight = availableWidth - 4 - (needsOverflow ? _menuButton.Width + 4 : 0);
            _addressGoButton.Left = addressRight - _addressGoButton.Width;
            _addressGoButton.Top = 12;
            if (_isHibernated)
            {
                _urlDragButton.Visible = false;
                _addressBox.Visible = false;
                _statusLabel.Visible = true;
                _statusLabel.Left = left + 4;
                _statusLabel.Top = 3;
                _statusLabel.Height = 42;
                _statusLabel.Width = Math.Max(80, _addressGoButton.Left - _statusLabel.Left - 4);
                _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            }
            else
            {
                _urlDragButton.Visible = true;
                _urlDragButton.Left = left + 2;
                _urlDragButton.Top = 12;
                _addressBox.Visible = true;
                _addressBox.Left = _urlDragButton.Right + 2;
                _addressBox.Top = 13;
                _addressBox.Width = Math.Max(120, _addressGoButton.Left - _addressBox.Left - 2);
                _statusLabel.Visible = false;
            }
        }

        public void PrepareStartDashboard()
        {
            if (Record.IsQuickConsult || _isHibernated || _startPanel == null ||
                _app.State.Settings.ShowStartDashboard != true)
                return;

            _startDashboardVisible = true;
            _startAddressEditPrepared = false;
            _startDashboardSecondsRemaining = Math.Max(5, _app.State.Settings.StartDashboardTimeoutSeconds);
            RefreshStartDashboard();
            _sleepPanel.Visible = false;
            _startPanel.Visible = true;
            _startPanel.BringToFront();
            UpdateStartDashboardCountdown();
        }

        private void BeginStartDashboardCountdown()
        {
            if (!_startDashboardVisible)
                return;
            _startDashboardSecondsRemaining = Math.Max(5, _app.State.Settings.StartDashboardTimeoutSeconds);
            if (_startStopButton != null)
                _startStopButton.Enabled = true;
            UpdateStartDashboardCountdown();
            _startDashboardTimer.Start();
        }

        private void StopStartDashboardCountdown()
        {
            if (_startDashboardTimer != null)
                _startDashboardTimer.Stop();
            if (_startStopButton != null)
                _startStopButton.Enabled = false;
            if (_startCountdownLabel != null)
                _startCountdownLabel.Text = "Apertura automática detenida · elige una página";
        }

        private void StartDashboardTimerTick(object sender, EventArgs args)
        {
            if (!_startDashboardVisible || IsDisposed)
            {
                _startDashboardTimer.Stop();
                return;
            }

            _startDashboardSecondsRemaining--;
            if (_startDashboardSecondsRemaining <= 0)
            {
                StartBrowserFromDashboard(AppController.HomeUrl);
                return;
            }
            UpdateStartDashboardCountdown();
        }

        private void UpdateStartDashboardCountdown()
        {
            if (_startCountdownLabel == null)
                return;
            int seconds = Math.Max(0, _startDashboardSecondsRemaining);
            _startCountdownLabel.Text = "ChatGPT opens in " + seconds.ToString() + " second" + (seconds == 1 ? string.Empty : "s");
        }

        private void StartBrowserFromDashboard(string url)
        {
            if (!_startDashboardVisible)
            {
                Navigate(url);
                return;
            }

            Record.Url = NormalizeNavigationUrl(url);
            _startAddressEditPrepared = false;
            if (_addressBox != null && !_addressBox.Focused)
                _addressBox.Text = Record.Url;
            _startDashboardVisible = false;
            _startDashboardTimer.Stop();
            _startPanel.Visible = false;
            _browserHost.BringToFront();
            if (Record.BookmarksVisible)
                _bookmarksPanel.BringToFront();
            BeginRestore();
        }

        private Panel CreateStartDashboard(
            out FlowLayoutPanel frequentTiles,
            out FlowLayoutPanel recentTiles,
            out Label countdown)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(0);

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 176;
            header.Padding = new Padding(42, 14, 42, 10);

            PictureBox appIcon = new PictureBox();
            appIcon.Size = new Size(68, 68);
            appIcon.Location = new Point(0, 3);
            appIcon.SizeMode = PictureBoxSizeMode.CenterImage;
            appIcon.Image = CreateStartApplicationIcon();
            _startDashboardImages.Add(appIcon.Image);

            Label heading = new Label();
            heading.Name = "StartDashboardHeading";
            heading.Text = AppController.ProductName;
            heading.Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold);
            heading.AutoSize = true;
            heading.Location = new Point(84, 1);

            Label subtitle = new Label();
            subtitle.Name = "StartDashboardSubtitle";
            subtitle.Text = "Choose where to start";
            subtitle.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
            subtitle.AutoSize = true;
            subtitle.Location = new Point(86, 34);

            Label versionLabel = new Label();
            versionLabel.Name = "StartDashboardVersion";
            versionLabel.Text = "Fast local start  ·  bookmarks and recent pages stay on this PC  ·  v" + AppController.ProductVersion;
            versionLabel.Font = new Font("Segoe UI", 8.5f);
            versionLabel.AutoSize = true;
            versionLabel.Location = new Point(86, 55);

            Label countdownLabel = new Label();
            countdownLabel.Name = "StartDashboardCountdown";
            countdownLabel.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            countdownLabel.AutoSize = true;
            countdownLabel.Location = new Point(86, 78);
            countdown = countdownLabel;

            _startStopButton = new Button();
            _startStopButton.Text = "⏸ Stop";
            _startStopButton.AutoSize = true;
            _startStopButton.Height = 27;
            _startStopButton.Location = new Point(278, 74);
            _startStopButton.Click += delegate { StopStartDashboardCountdown(); };

            _startSearchBox = new TextBox();
            _startSearchBox.Name = "StartDashboardSearch";
            _startSearchBox.Location = new Point(108, 111);
            _startSearchBox.Width = 610;
            _startSearchBox.Height = 30;
            _startSearchBox.Font = new Font("Segoe UI", 10f);
            _startSearchBox.BorderStyle = BorderStyle.FixedSingle;
            _startSearchBox.AccessibleName = "Search bookmarks, recent pages or Google";
            _startSearchBox.Text = string.Empty;
            _startSearchBox.TextChanged += delegate
            {
                if (_startDashboardVisible && _startSearchBox.Focused)
                    StopStartDashboardCountdown();
                RefreshStartDashboard();
            };
            _startSearchBox.Enter += delegate
            {
                if (_startDashboardVisible)
                    StopStartDashboardCountdown();
            };
            _startSearchBox.MouseDown += delegate
            {
                if (_startDashboardVisible)
                    StopStartDashboardCountdown();
            };
            _startSearchBox.KeyDown += StartDashboardSearchKeyDown;

            Label searchLabel = new Label();
            searchLabel.Text = "⌕";
            searchLabel.Font = new Font("Segoe UI Symbol", 17f, FontStyle.Regular);
            searchLabel.AutoSize = true;
            searchLabel.Location = new Point(78, 108);

            Label searchHint = new Label();
            searchHint.Text = "Bookmark, recent page, URL or Google search";
            searchHint.Font = new Font("Segoe UI", 8.5f);
            searchHint.AutoSize = true;
            searchHint.Location = new Point(110, 143);

            header.Controls.Add(appIcon);
            header.Controls.Add(heading);
            header.Controls.Add(subtitle);
            header.Controls.Add(versionLabel);
            header.Controls.Add(countdownLabel);
            header.Controls.Add(_startStopButton);
            header.Controls.Add(_startSearchBox);
            header.Controls.Add(searchLabel);
            header.Controls.Add(searchHint);
            header.Resize += delegate
            {
                if (_startSearchBox == null || _startSearchBox.IsDisposed)
                    return;
                _startSearchBox.Width = Math.Max(260, header.ClientSize.Width - 150);
            };

            Panel content = new Panel();
            content.Name = "StartDashboardContent";
            content.Dock = DockStyle.Fill;
            content.AutoScroll = true;
            content.Padding = new Padding(42, 6, 30, 16);

            FlowLayoutPanel frequent = CreateStartTileRow();
            frequent.Name = "StartDashboardFrequent";
            frequentTiles = frequent;

            Label frequentHeading = CreateStartSectionHeading("Most used and bookmarks");
            _startFrequentHeading = frequentHeading;

            FlowLayoutPanel recent = CreateStartTileRow();
            recent.Name = "StartDashboardRecent";
            recentTiles = recent;

            Label recentHeading = CreateStartSectionHeading("Recently visited");
            _startRecentHeading = recentHeading;

            content.Controls.Add(recent);
            content.Controls.Add(recentHeading);
            content.Controls.Add(frequent);
            content.Controls.Add(frequentHeading);

            panel.Controls.Add(content);
            panel.Controls.Add(header);
            panel.Resize += delegate { LayoutStartDashboardRows(); };
            return panel;
        }

        private static FlowLayoutPanel CreateStartTileRow()
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.Dock = DockStyle.Top;
            row.Height = 126;
            row.WrapContents = true;
            row.AutoScroll = false;
            row.FlowDirection = FlowDirection.LeftToRight;
            row.Padding = new Padding(0, 0, 0, 5);
            return row;
        }

        private static Label CreateStartSectionHeading(string text)
        {
            Label heading = new Label();
            heading.Text = text;
            heading.Dock = DockStyle.Top;
            heading.Height = 27;
            heading.Padding = new Padding(2, 6, 0, 0);
            heading.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
            return heading;
        }

        private void RefreshStartDashboard()
        {
            if (_startFrequentTiles == null || _startRecentTiles == null)
                return;

            ClearStartTiles(_startFrequentTiles);
            ClearStartTiles(_startRecentTiles);

            string search = (_startSearchBox == null ? string.Empty : _startSearchBox.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                if (_startFrequentHeading != null)
                    _startFrequentHeading.Text = "Bookmarks encontrados";
                if (_startRecentHeading != null)
                    _startRecentHeading.Text = "Historial coincidente";

                foreach (BookmarkRecord bookmark in _app.State.Bookmarks
                    .Where(delegate(BookmarkRecord item) { return MatchesStartSearch(item == null ? string.Empty : (item.Title + " " + item.FolderPath), item == null ? string.Empty : item.Url, search); })
                    .OrderBy(delegate(BookmarkRecord item) { return item.Title ?? item.Url; })
                    .Take(12))
                    AddStartTile(_startFrequentTiles, bookmark);

                foreach (RecentRecord recent in _app.State.Recents
                    .Where(delegate(RecentRecord item) { return item != null && MatchesStartSearch(item.Title, item.Url, search); })
                    .OrderByDescending(delegate(RecentRecord item) { return item.LastOpenedUtc; })
                    .Take(8))
                    AddStartTile(_startRecentTiles, recent);

                if (_startFrequentTiles.Controls.Count == 0 && _startRecentTiles.Controls.Count == 0)
                {
                    Label emptySearch = new Label();
                    emptySearch.Text = "No se encontró ningún bookmark o página";
                    emptySearch.AutoSize = true;
                    emptySearch.Margin = new Padding(4, 18, 0, 0);
                    emptySearch.Font = new Font("Segoe UI", 9f, FontStyle.Italic);
                    _startFrequentTiles.Controls.Add(emptySearch);
                }
                ApplyStartDashboardTheme();
                LayoutStartDashboardRows();
                return;
            }

            if (_startFrequentHeading != null)
                _startFrequentHeading.Text = "Most used and bookmarks";
            if (_startRecentHeading != null)
                _startRecentHeading.Text = "Recently visited";

            List<RecentRecord> mostUsed = _app.GetMostUsedStartPages(6).ToList();
            RecentRecord chatGpt = mostUsed.FirstOrDefault(delegate(RecentRecord item)
            {
                return string.Equals(NormalizeNavigationUrl(item.Url), AppController.HomeUrl, StringComparison.OrdinalIgnoreCase);
            });
            if (chatGpt == null)
            {
                chatGpt = new RecentRecord();
                chatGpt.Url = AppController.HomeUrl;
                chatGpt.Title = "ChatGPT";
            }

            HashSet<string> shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddStartTile(_startFrequentTiles, chatGpt);
            shown.Add(chatGpt.Url);
            foreach (BookmarkRecord bookmark in _app.State.Bookmarks
                .Where(delegate(BookmarkRecord item) { return item != null && !string.IsNullOrWhiteSpace(item.Url); })
                .OrderByDescending(delegate(BookmarkRecord item) { return item.CreatedUtc; })
                .Take(8))
            {
                if (shown.Contains(bookmark.Url))
                    continue;
                AddStartTile(_startFrequentTiles, bookmark);
                shown.Add(bookmark.Url);
            }
            foreach (RecentRecord page in mostUsed)
            {
                if (page == null || string.IsNullOrWhiteSpace(page.Url) || shown.Contains(page.Url))
                    continue;
                AddStartTile(_startFrequentTiles, page);
                shown.Add(page.Url);
            }

            List<RecentRecord> recentPages = _app.GetRecentStartPages(6, shown).ToList();
            foreach (RecentRecord page in recentPages)
            {
                AddStartTile(_startRecentTiles, page);
                shown.Add(page.Url);
            }
            if (_startRecentTiles.Controls.Count == 0)
            {
                Label empty = new Label();
                empty.Text = "Visited pages will appear here.";
                empty.AutoSize = true;
                empty.Margin = new Padding(4, 18, 0, 0);
                empty.Font = new Font("Segoe UI", 9f, FontStyle.Italic);
                _startRecentTiles.Controls.Add(empty);
            }

            ApplyStartDashboardTheme();
            LayoutStartDashboardRows();
        }

        private void ClearStartTiles(FlowLayoutPanel row)
        {
            foreach (Control control in row.Controls)
            {
                foreach (PictureBox picture in control.Controls.OfType<PictureBox>())
                {
                    if (picture.Image != null)
                    {
                        Image image = picture.Image;
                        picture.Image = null;
                        _startDashboardImages.Remove(image);
                        image.Dispose();
                    }
                }
                control.Dispose();
            }
            row.Controls.Clear();
        }

        private void StartDashboardSearchKeyDown(object sender, KeyEventArgs args)
        {
            if (args.KeyCode != Keys.Enter)
                return;

            string query = (_startSearchBox == null ? string.Empty : _startSearchBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
                return;

            BookmarkRecord bookmark = _app.State.Bookmarks
                .Where(delegate(BookmarkRecord item) { return MatchesStartSearch(item == null ? string.Empty : (item.Title + " " + item.FolderPath), item == null ? string.Empty : item.Url, query); })
                .OrderBy(delegate(BookmarkRecord item) { return item.Title ?? item.Url; })
                .FirstOrDefault();
            if (bookmark != null && !string.IsNullOrWhiteSpace(bookmark.Url))
            {
                StartBrowserFromDashboard(bookmark.Url);
                args.SuppressKeyPress = true;
                args.Handled = true;
                return;
            }

            RecentRecord recent = _app.State.Recents
                .Where(delegate(RecentRecord item) { return item != null && MatchesStartSearch(item.Title, item.Url, query); })
                .OrderByDescending(delegate(RecentRecord item) { return item.LastOpenedUtc; })
                .FirstOrDefault();
            if (recent != null && !string.IsNullOrWhiteSpace(recent.Url))
            {
                StartBrowserFromDashboard(recent.Url);
                args.SuppressKeyPress = true;
                args.Handled = true;
                return;
            }

            Uri direct;
            if (Uri.TryCreate(query, UriKind.Absolute, out direct) &&
                (direct.Scheme == Uri.UriSchemeHttp || direct.Scheme == Uri.UriSchemeHttps))
            {
                StartBrowserFromDashboard(direct.AbsoluteUri);
                args.SuppressKeyPress = true;
                args.Handled = true;
            }
        }

        private static bool MatchesStartSearch(string title, string url, string query)
        {
            string needle = NormalizeStartSearchText(query);
            if (string.IsNullOrWhiteSpace(needle))
                return false;
            return NormalizeStartSearchText(title).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   NormalizeStartSearchText(url).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizeStartSearchText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char character in value.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                    builder.Append(character);
            }
            return builder.ToString();
        }

        private void AddStartTile(FlowLayoutPanel row, BookmarkRecord bookmark)
        {
            if (bookmark == null || string.IsNullOrWhiteSpace(bookmark.Url))
                return;
            RecentRecord page = new RecentRecord();
            page.Url = bookmark.Url;
            page.Title = bookmark.Title;
            page.VisitCount = 0;
            AddStartTile(row, page);
        }

        private void AddStartTile(FlowLayoutPanel row, RecentRecord page)
        {
            if (page == null || string.IsNullOrWhiteSpace(page.Url))
                return;

            string url = page.Url;
            Panel tile = new Panel();
            tile.Width = 106;
            tile.Height = 112;
            tile.Margin = new Padding(2, 2, 10, 4);
            tile.Padding = new Padding(5, 5, 5, 4);
            tile.Cursor = Cursors.Hand;
            tile.Tag = false;
            tile.AccessibleName = "Open " + StartTileTitle(page);

            PictureBox icon = new PictureBox();
            icon.Size = new Size(58, 58);
            icon.Location = new Point((tile.Width - icon.Width) / 2, 5);
            icon.SizeMode = PictureBoxSizeMode.CenterImage;
            icon.Image = CreateStartSiteIcon(url);
            _startDashboardImages.Add(icon.Image);
            QueueStartSiteFavicon(url, icon);

            Label title = new Label();
            title.Text = StartTileTitle(page);
            title.AutoEllipsis = true;
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.Location = new Point(2, 67);
            title.Size = new Size(tile.Width - 4, 19);
            title.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);

            Label details = new Label();
            details.Text = page.VisitCount > 1 ? page.VisitCount.ToString() + " visits" : StartTileHost(url);
            details.AutoEllipsis = true;
            details.TextAlign = ContentAlignment.MiddleCenter;
            details.Location = new Point(2, 87);
            details.Size = new Size(tile.Width - 4, 17);
            details.Font = new Font("Segoe UI", 7.5f);

            tile.Controls.Add(icon);
            tile.Controls.Add(title);
            tile.Controls.Add(details);
            AttachStartTileHandler(tile, url, tile);
            AttachStartTileHandler(icon, url, tile);
            AttachStartTileHandler(title, url, tile);
            AttachStartTileHandler(details, url, tile);
            row.Controls.Add(tile);
            UpdateStartTileStyle(tile, false);
        }

        private void AttachStartTileHandler(Control control, string url, Panel tile)
        {
            control.Click += delegate { StartBrowserFromDashboard(url); };
            control.MouseEnter += delegate { UpdateStartTileStyle(tile, true); };
            control.MouseLeave += delegate
            {
                if (!tile.ClientRectangle.Contains(tile.PointToClient(Cursor.Position)))
                    UpdateStartTileStyle(tile, false);
            };
        }

        private void LayoutStartDashboardRows()
        {
            if (_startPanel == null || _startFrequentTiles == null || _startRecentTiles == null)
                return;
            int usableWidth = Math.Max(320, _startPanel.ClientSize.Width - 80);
            int columns = Math.Max(3, usableWidth / 118);
            int frequentRows = Math.Max(1, (_startFrequentTiles.Controls.Count + columns - 1) / columns);
            int recentRows = Math.Max(1, (_startRecentTiles.Controls.Count + columns - 1) / columns);
            _startFrequentTiles.Height = frequentRows * 118 + 6;
            _startRecentTiles.Height = recentRows * 118 + 6;
        }

        private void ApplyStartDashboardTheme()
        {
            if (_startPanel == null)
                return;
            bool dark = _app.State.Settings.DarkMode;
            _startPanel.BackColor = dark ? UiTheme.DarkBackground : Color.FromArgb(242, 246, 251);
            UiTheme.ApplyNativeTree(_startPanel, dark);
            _startPanel.BackColor = dark ? UiTheme.DarkBackground : Color.FromArgb(242, 246, 251);
            foreach (Control row in new Control[] { _startFrequentTiles, _startRecentTiles })
            {
                if (row == null)
                    continue;
                row.BackColor = _startPanel.BackColor;
                foreach (Control tile in row.Controls)
                {
                    Panel tilePanel = tile as Panel;
                    if (tilePanel != null)
                        UpdateStartTileStyle(tilePanel, tilePanel.Tag is bool && (bool)tilePanel.Tag);
                    else if (tile is Label)
                        tile.ForeColor = dark ? UiTheme.DarkMutedText : Color.FromArgb(95, 95, 95);
                }
            }
            if (_startCountdownLabel != null)
                _startCountdownLabel.ForeColor = dark ? Color.FromArgb(142, 193, 255) : Color.FromArgb(51, 98, 170);
            if (_startStopButton != null)
            {
                _startStopButton.BackColor = dark ? UiTheme.DarkInput : Color.White;
                _startStopButton.ForeColor = dark ? UiTheme.DarkText : UiTheme.LightText;
                _startStopButton.FlatStyle = FlatStyle.Flat;
                _startStopButton.FlatAppearance.BorderColor = dark ? Color.FromArgb(90, 105, 124) : Color.FromArgb(170, 180, 194);
            }
            if (_startSearchBox != null)
            {
                _startSearchBox.BackColor = dark ? UiTheme.DarkInput : Color.White;
                _startSearchBox.ForeColor = dark ? UiTheme.DarkText : UiTheme.LightText;
            }
        }

        private void UpdateStartTileStyle(Panel tile, bool hover)
        {
            if (tile == null || tile.IsDisposed)
                return;
            tile.Tag = hover;
            bool dark = _app.State.Settings.DarkMode;
            tile.BackColor = dark
                ? (hover ? Color.FromArgb(56, 71, 89) : Color.FromArgb(39, 45, 52))
                : (hover ? Color.FromArgb(226, 239, 255) : Color.White);
            tile.BorderStyle = BorderStyle.FixedSingle;
            tile.ForeColor = dark ? UiTheme.DarkText : UiTheme.LightText;
            foreach (Control child in tile.Controls)
            {
                Label label = child as Label;
                if (label != null)
                    label.ForeColor = label.Top > 80
                        ? (dark ? UiTheme.DarkMutedText : Color.FromArgb(90, 90, 90))
                        : (dark ? UiTheme.DarkText : UiTheme.LightText);
            }
        }

        private Bitmap CreateStartApplicationIcon()
        {
            Bitmap savedIcon = IconVisuals.CreateImageBitmap(Record, 68, 68);
            if (savedIcon != null)
                return savedIcon;
            Bitmap image = new Bitmap(68, 68, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                IconVisuals.DrawBadge(graphics, new Rectangle(2, 2, 64, 64), IconVisuals.GetGlyph(Record), IconVisuals.GetColor(Record));
            }
            return image;
        }

        private Bitmap CreateStartSiteIcon(string url)
        {
            WindowRecord saved = _app.State.Windows
                .Where(delegate(WindowRecord item)
                {
                    return item != null && string.Equals(NormalizeNavigationUrl(item.Url), NormalizeNavigationUrl(url), StringComparison.OrdinalIgnoreCase) &&
                           IconVisuals.HasImage(item);
                })
                .OrderByDescending(delegate(WindowRecord item) { return item.LastActiveUtc; })
                .FirstOrDefault();
            Bitmap favicon = IconVisuals.CreateImageBitmap(saved, 58, 58);
            if (favicon != null)
                return favicon;

            Bitmap cachedFavicon = CreateStartCachedIcon(_app.GetSiteFaviconPath(url));
            if (cachedFavicon != null)
                return cachedFavicon;

            string host = StartTileHost(url).ToLowerInvariant();
            string glyph = "â—†";
            Color accent = Color.FromArgb(71, 115, 184);
            if (host.Contains("chatgpt") || host.Contains("openai"))
            {
                glyph = "âœ¦";
                accent = Color.FromArgb(26, 122, 102);
            }
            else if (host.Contains("gemini"))
            {
                glyph = "âœ¦";
                accent = Color.FromArgb(78, 115, 217);
            }
            else if (host.Contains("gmail"))
            {
                glyph = "M";
                accent = Color.FromArgb(210, 62, 54);
            }
            else if (host.Contains("youtube"))
            {
                glyph = "â–¶";
                accent = Color.FromArgb(226, 34, 44);
            }
            else if (host.Contains("whatsapp"))
            {
                glyph = "â˜Ž";
                accent = Color.FromArgb(37, 167, 95);
            }
            else if (host.Contains("google"))
            {
                glyph = "G";
                accent = Color.FromArgb(57, 112, 205);
            }
            else if (host.Contains("duck"))
            {
                glyph = "D";
                accent = Color.FromArgb(219, 97, 41);
            }
            else
            {
                glyph = "â—‰";
                int colorIndex = 0;
                foreach (char character in host)
                    colorIndex = (colorIndex * 31 + character) & 0x7fffffff;
                Color[] colors = new Color[]
                {
                    Color.FromArgb(73, 112, 187), Color.FromArgb(144, 82, 182),
                    Color.FromArgb(41, 151, 132), Color.FromArgb(191, 97, 63),
                    Color.FromArgb(177, 130, 42)
                };
                accent = colors[colorIndex % colors.Length];
            }

            Bitmap image = new Bitmap(58, 58, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(image))
            using (Brush white = new SolidBrush(Color.FromArgb(246, 248, 250)))
            using (Brush border = new SolidBrush(Color.FromArgb(224, 229, 235)))
            using (Font font = new Font("Segoe UI Symbol", 22f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.FillEllipse(border, new Rectangle(1, 1, 56, 56));
                graphics.FillEllipse(white, new Rectangle(2, 2, 54, 54));
                TextRenderer.DrawText(
                    graphics,
                    glyph,
                    font,
                    new Rectangle(4, 4, 50, 50),
                    accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            return image;
        }

        private bool HasStartSiteFavicon(string url)
        {
            string cachePath = _app.GetSiteFaviconPath(url);
            if (!string.IsNullOrWhiteSpace(cachePath) && File.Exists(cachePath))
                return true;
            return _app.State.Windows.Any(delegate(WindowRecord item)
            {
                return item != null &&
                    string.Equals(NormalizeNavigationUrl(item.Url), NormalizeNavigationUrl(url), StringComparison.OrdinalIgnoreCase) &&
                    IconVisuals.HasImage(item);
            });
        }

        private void QueueStartSiteFavicon(string url, PictureBox icon)
        {
            if (icon == null || string.IsNullOrWhiteSpace(url) || HasStartSiteFavicon(url))
                return;
            string cachePath = _app.GetSiteFaviconPath(url);
            if (string.IsNullOrWhiteSpace(cachePath) || !_startFaviconRequests.Add(cachePath))
                return;
            BeginStartFaviconDownload(url, cachePath, icon);
        }

        private async void BeginStartFaviconDownload(string url, string cachePath, PictureBox icon)
        {
            try
            {
                await StartFaviconDownloadSlots.WaitAsync();
                try
                {
                    if (IsDisposed)
                        return;
                    bool saved = await TryDownloadFaviconAsync(BuildFaviconServiceUri(url), cachePath);
                    if (!saved || IsDisposed || icon == null || icon.IsDisposed)
                        return;
                    // RefreshStartDashboard can run before the form is shown. The
                    // continuation resumes on the WinForms UI context, so updating
                    // the tile directly also works before a native handle exists.
                    // Previously the !IsHandleCreated guard discarded the favicon
                    // result and left the first dashboard showing only a letter.
                    ReplaceStartTileIcon(icon, cachePath);
                }
                finally
                {
                    StartFaviconDownloadSlots.Release();
                }
            }
            catch
            {
                // Missing favicons keep the compact neutral fallback and do not affect browsing.
            }
            finally
            {
                _startFaviconRequests.Remove(cachePath);
            }
        }

        private static Bitmap CreateStartCachedIcon(string cachePath)
        {
            if (string.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath))
                return null;
            try
            {
                Bitmap result = new Bitmap(58, 58, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(result))
                {
                    graphics.Clear(Color.Transparent);
                    if (!IconVisuals.DrawImage(graphics, cachePath, new Rectangle(2, 2, 54, 54)))
                    {
                        result.Dispose();
                        return null;
                    }
                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        private void ReplaceStartTileIcon(PictureBox icon, string cachePath)
        {
            if (icon == null || icon.IsDisposed || IsDisposed || string.IsNullOrWhiteSpace(cachePath))
                return;
            Bitmap replacement = CreateStartCachedIcon(cachePath);
            if (replacement == null)
                return;
            Image previous = icon.Image;
            icon.Image = replacement;
            _startDashboardImages.Add(replacement);
            if (previous != null)
            {
                _startDashboardImages.Remove(previous);
                previous.Dispose();
            }
        }

        private static string StartTileTitle(RecentRecord page)
        {
            string title = page == null ? string.Empty : page.Title;
            if (string.IsNullOrWhiteSpace(title))
                title = StartTileHost(page == null ? string.Empty : page.Url);
            title = title.Trim();
            return title.Length > 17 ? title.Substring(0, 16) + "â€¦" : title;
        }

        private static string StartTileHost(string url)
        {
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                string host = uri.Host;
                return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host.Substring(4) : host;
            }
            return "Page";
        }

        private Panel CreateNotesPanel(
            out RichTextBox editor,
            out Label heading,
            out FlowLayoutPanel formatBar,
            out Panel header,
            out Label footer,
            out Button close)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Right;
            panel.Width = 310;
            panel.Padding = new Padding(1);

            header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 38;

            heading = new Label();
            heading.Dock = DockStyle.Fill;
            heading.Padding = new Padding(12, 0, 4, 0);
            heading.TextAlign = ContentAlignment.MiddleLeft;
            heading.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            heading.Text = NotesHeadingText();

            close = new Button();
            close.Text = "×";
            close.Dock = DockStyle.Right;
            close.Width = 38;
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderSize = 0;
            close.Font = new Font("Segoe UI", 14f);
            close.Cursor = Cursors.Hand;
            close.Click += delegate { SetNotesVisible(false); };

            header.Controls.Add(heading);
            header.Controls.Add(close);

            footer = new Label();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 28;
            footer.Padding = new Padding(12, 0, 8, 0);
            footer.TextAlign = ContentAlignment.MiddleLeft;
            footer.Font = new Font("Segoe UI", 8f);
            footer.Text = "Nota RTF local · arrastra imágenes dentro del texto";

            Panel noteBody = new Panel();
            noteBody.Dock = DockStyle.Fill;

            RichTextBox noteEditor = new RichTextBox();
            noteEditor.Dock = DockStyle.Fill;
            noteEditor.BorderStyle = BorderStyle.None;
            noteEditor.Font = new Font("Segoe UI", 10f);
            noteEditor.DetectUrls = true;
            noteEditor.AllowDrop = true;
            noteEditor.DragEnter += NoteEditorDragEnter;
            noteEditor.DragDrop += NoteEditorDragDrop;

            formatBar = new FlowLayoutPanel();
            formatBar.Dock = DockStyle.Top;
            formatBar.Height = 34;
            formatBar.WrapContents = false;
            formatBar.Padding = new Padding(6, 3, 4, 3);
            formatBar.FlowDirection = FlowDirection.LeftToRight;

            Button bold = CreateNoteFormatButton("B", 28, FontStyle.Bold);
            bold.Click += delegate { ToggleNoteFontStyle(FontStyle.Bold); };
            Button italic = CreateNoteFormatButton("I", 28, FontStyle.Italic);
            italic.Click += delegate { ToggleNoteFontStyle(FontStyle.Italic); };
            Button color = CreateNoteFormatButton("🎨", 40, FontStyle.Regular);
            color.Click += delegate { ChooseNoteTextColor(); };
            Button markdown = CreateNoteFormatButton("MD", 38, FontStyle.Regular);
            markdown.Click += delegate { WrapSelectionInMarkdown(); };
            Button image = CreateNoteFormatButton("🖼", 42, FontStyle.Regular);
            image.Click += delegate { ChooseAndInsertImages(); };
            formatBar.Controls.Add(bold);
            formatBar.Controls.Add(italic);
            formatBar.Controls.Add(color);
            formatBar.Controls.Add(markdown);
            formatBar.Controls.Add(image);

            _app.LoadNoteDocument(Record, noteEditor);
            noteEditor.TextChanged += delegate
            {
                Record.NoteText = noteEditor.Text;
                _noteDocumentDirty = true;
                _app.QueueSave();
            };

            editor = noteEditor;
            noteBody.Controls.Add(noteEditor);
            noteBody.Controls.Add(formatBar);
            panel.Controls.Add(noteBody);
            panel.Controls.Add(footer);
            panel.Controls.Add(header);
            return panel;
        }

        private Panel CreateBookmarksPanel(
            out TreeView tree,
            out Panel header,
            out Label heading,
            out Button close)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Left;
            panel.Width = 285;
            panel.Padding = new Padding(1);

            header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 38;

            heading = new Label();
            heading.Dock = DockStyle.Fill;
            heading.Padding = new Padding(11, 0, 4, 0);
            heading.TextAlign = ContentAlignment.MiddleLeft;
            heading.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            heading.Text = "Bookmarks";

            close = new Button();
            close.Text = "×";
            close.Dock = DockStyle.Right;
            close.Width = 38;
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderSize = 0;
            close.Font = new Font("Segoe UI", 14f);
            close.Cursor = Cursors.Hand;
            close.Click += delegate { SetBookmarksVisible(false); };

            TreeView bookmarkTree = new TreeView();
            bookmarkTree.Dock = DockStyle.Fill;
            bookmarkTree.BorderStyle = BorderStyle.FixedSingle;
            bookmarkTree.FullRowSelect = true;
            bookmarkTree.HideSelection = false;
            bookmarkTree.HotTracking = false;
            bookmarkTree.Indent = 20;
            bookmarkTree.ItemHeight = 22;
            bookmarkTree.ShowLines = true;
            bookmarkTree.ShowRootLines = true;
            bookmarkTree.ShowPlusMinus = true;
            bookmarkTree.ShowRootLines = true;
            bookmarkTree.NodeMouseDoubleClick += delegate(object sender, TreeNodeMouseClickEventArgs args)
            {
                BookmarkRecord bookmark = args.Node == null ? null : args.Node.Tag as BookmarkRecord;
                if (bookmark != null && !string.IsNullOrWhiteSpace(bookmark.Url))
                    Navigate(bookmark.Url);
            };

            header.Controls.Add(heading);
            header.Controls.Add(close);
            panel.Controls.Add(bookmarkTree);
            panel.Controls.Add(header);
            tree = bookmarkTree;
            return panel;
        }

        private void NoteEditorDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Bitmap)))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void NoteEditorDragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null)
                return;

            RichTextBox dropTarget = sender as RichTextBox;
            if (dropTarget != null)
            {
                Point location = dropTarget.PointToClient(new Point(e.X, e.Y));
                dropTarget.SelectionStart = dropTarget.GetCharIndexFromPosition(location);
                dropTarget.SelectionLength = 0;
                dropTarget.Focus();
            }

            int imported = 0;
            List<string> errors = new List<string>();
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null)
                    {
                        foreach (string file in files)
                        {
                            try
                            {
                                InsertImageIntoNote(file);
                                imported++;
                            }
                            catch (Exception ex)
                            {
                                errors.Add(Path.GetFileName(file) + ": " + ex.Message);
                            }
                        }
                    }
                }
                else if (e.Data.GetDataPresent(DataFormats.Bitmap))
                {
                    Image image = e.Data.GetData(DataFormats.Bitmap) as Image;
                    if (image != null)
                    {
                        InsertImageIntoNote(image, "Imagen pegada");
                        imported++;
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }

            if (imported > 0)
            {
                MarkNoteDocumentChanged();
                _app.QueueSave();
            }
            if (errors.Count > 0)
            {
                MessageBox.Show(
                    "No se pudo adjuntar una o más imágenes.\r\n\r\n" + string.Join("\r\n", errors.Take(4).ToArray()),
                    "Notas",
                    MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            }
        }

        private static Button CreateNoteFormatButton(string text, int width, FontStyle style)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 27;
            button.Margin = new Padding(0, 0, 4, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Font = new Font("Segoe UI", 8.5f, style);
            button.Cursor = Cursors.Hand;
            return button;
        }

        private void ToggleNoteFontStyle(FontStyle requestedStyle)
        {
            Font current = _notesEditor.SelectionFont ?? _notesEditor.Font;
            FontStyle style = current.Style;
            if ((style & requestedStyle) == requestedStyle)
                style &= ~requestedStyle;
            else
                style |= requestedStyle;
            _notesEditor.SelectionFont = new Font(current, style);
            MarkNoteDocumentChanged();
        }

        private void ChooseNoteTextColor()
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = _notesEditor.SelectionColor.IsEmpty ? Color.FromArgb(35, 35, 35) : _notesEditor.SelectionColor;
                dialog.FullOpen = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _notesEditor.SelectionColor = dialog.Color;
                    MarkNoteDocumentChanged();
                }
            }
        }

        private void WrapSelectionInMarkdown()
        {
            string selected = _notesEditor.SelectedText;
            if (string.IsNullOrEmpty(selected))
            {
                int start = _notesEditor.SelectionStart;
                _notesEditor.SelectedText = "**texto**";
                _notesEditor.SelectionStart = start + 2;
                _notesEditor.SelectionLength = 5;
            }
            else
            {
                _notesEditor.SelectedText = "**" + selected + "**";
            }
            MarkNoteDocumentChanged();
        }

        private void ChooseAndInsertImages()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Insertar imágenes en la nota";
                dialog.Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.gif;*.bmp";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                List<string> errors = new List<string>();
                int inserted = 0;
                foreach (string path in dialog.FileNames)
                {
                    try
                    {
                        InsertImageIntoNote(path);
                        inserted++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add(Path.GetFileName(path) + ": " + ex.Message);
                    }
                }
                if (inserted > 0)
                    MarkNoteDocumentChanged();
                if (errors.Count > 0)
                    MessageBox.Show("No se pudo insertar una o más imágenes.\r\n\r\n" + string.Join("\r\n", errors.Take(4).ToArray()), "Notas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InsertImageIntoNote(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("No se encontró la imagen.");

            FileInfo file = new FileInfo(path);
            if (file.Length > 20L * 1024L * 1024L)
                throw new InvalidOperationException("Cada imagen puede pesar hasta 20 MB.");

            string extension = file.Extension.ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".gif" && extension != ".bmp")
                throw new InvalidOperationException("Usa imágenes PNG, JPG, GIF o BMP.");

            using (Image image = Image.FromFile(path))
                InsertImageIntoNote(image, file.Name);
        }

        private void InsertImageIntoNote(Image source, string name)
        {
            if (source == null)
                throw new InvalidOperationException("No hay una imagen para insertar.");
            if ((long)source.Width * source.Height > 36000000L)
                throw new InvalidOperationException("La resolución máxima por imagen es de 36 megapíxeles.");

            int availableWidth = Math.Max(220, _notesEditor.ClientSize.Width - 24);
            int maximumWidth = Math.Min(520, availableWidth);
            double scale = Math.Min(1d, (double)maximumWidth / Math.Max(1, source.Width));
            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));

            using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, width, height));
                IDataObject previousClipboard = Clipboard.GetDataObject();
                try
                {
                    Clipboard.SetImage(bitmap);
                    _notesEditor.Paste(DataFormats.GetFormat(DataFormats.Bitmap));
                }
                finally
                {
                    RestoreClipboard(previousClipboard);
                }
            }
            MarkNoteDocumentChanged();
        }

        private static void RestoreClipboard(IDataObject previousClipboard)
        {
            try
            {
                if (previousClipboard == null)
                    Clipboard.Clear();
                else
                    Clipboard.SetDataObject(previousClipboard, true);
            }
            catch
            {
            }
        }

        private void MarkNoteDocumentChanged()
        {
            _noteDocumentDirty = true;
            Record.NoteText = _notesEditor.Text ?? string.Empty;
            _app.QueueSave();
        }

        private void MigrateLegacyNoteImages()
        {
            List<NoteImageRecord> converted = new List<NoteImageRecord>();
            foreach (NoteImageRecord image in Record.NoteImages.ToArray())
            {
                try
                {
                    string path = _app.GetNoteImagePath(Record, image);
                    if (!File.Exists(path))
                        continue;
                    InsertImageIntoNote(path);
                    converted.Add(image);
                }
                catch
                {
                }
            }
            if (converted.Count > 0)
            {
                foreach (NoteImageRecord image in converted)
                    Record.NoteImages.Remove(image);
                MarkNoteDocumentChanged();
            }
        }

        private void SetNotesVisible(bool visible)
        {
            SetNotesVisible(visible, true);
        }

        private void SetNotesVisible(bool visible, bool save)
        {
            if (IsDisposed || Disposing || _notesPanel == null || _notesPanel.IsDisposed)
                return;
            Record.NotesVisible = visible;
            _notesPanel.Visible = visible;
            if (_notesButton != null && !_notesButton.IsDisposed)
            {
                _notesButton.FlatAppearance.BorderSize = visible ? 1 : 0;
                _notesButton.FlatAppearance.BorderColor = IconVisuals.ContrastColor(IconVisuals.GetColor(Record));
            }
            if (_notesPanel.Parent != null && !_notesPanel.Parent.IsDisposed)
                _notesPanel.Parent.PerformLayout();

            if (visible && save && WindowState == FormWindowState.Normal && Width < 900)
            {
                Rectangle workArea = Screen.FromControl(this).WorkingArea;
                Width = Math.Min(900, workArea.Width);
            }
            if (visible && save && Visible)
                _notesEditor.Focus();
            if (save)
                _app.WindowPresentationChanged();
        }

        private string NotesHeadingText()
        {
            string label = "Notas";
            if (!string.IsNullOrWhiteSpace(Record.Identifier))
                label += " · " + Record.Identifier.Trim();
            return label;
        }

        private static Button CreateToolbarButton(string text, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 50;
            button.Top = 2;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.Transparent;
            button.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            button.TextAlign = ContentAlignment.BottomCenter;
            button.TextImageRelation = TextImageRelation.ImageAboveText;
            button.ImageAlign = ContentAlignment.TopCenter;
            button.Padding = new Padding(0, 1, 0, 0);
            button.Cursor = Cursors.Hand;
            button.AccessibleRole = AccessibleRole.PushButton;
            button.TabStop = true;
            button.AutoEllipsis = true;
            return button;
        }

        private void SetToolbarButtonGlyph(Button button, string glyph)
        {
            if (button == null)
                return;
            string label = GetToolbarCaption(button.Tag as string);
            string actualGlyph = glyph ?? string.Empty;
            _toolbarGlyphs[button] = actualGlyph;
            Image previous = button.Image;
            button.Image = button == _appearanceButton
                ? IconVisuals.CreateImageBitmap(Record, 32, 28) ?? CreateToolbarGlyphImage(actualGlyph, button.ForeColor)
                : CreateToolbarGlyphImage(actualGlyph, button.ForeColor);
            if (previous != null)
                previous.Dispose();
            button.Text = label;
        }

        private static Bitmap CreateToolbarGlyphImage(string glyph, Color color)
        {
            Bitmap image = new Bitmap(32, 28, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(image))
            using (Font font = new Font("Segoe UI Symbol", 17.5f, FontStyle.Regular, GraphicsUnit.Point))
            {
                graphics.Clear(Color.Transparent);
                TextRenderer.DrawText(
                    graphics,
                    glyph ?? string.Empty,
                    font,
                    new Rectangle(0, 0, image.Width, image.Height),
                    color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            return image;
        }

        private static string GetToolbarCaption(string accessibleLabel)
        {
            if (string.IsNullOrWhiteSpace(accessibleLabel))
                return string.Empty;
            if (accessibleLabel.IndexOf("Title, ID", StringComparison.OrdinalIgnoreCase) >= 0)
                return "ID / icon";
            if (accessibleLabel.IndexOf("Copy mode", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Copy";
            if (accessibleLabel.IndexOf("Show or hide notes", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Notes";
            if (accessibleLabel.IndexOf("Keep this window awake", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Caffeine";
            if (accessibleLabel.IndexOf("Hibernate this window", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Sleep";
            if (accessibleLabel.IndexOf("New window", StringComparison.OrdinalIgnoreCase) >= 0)
                return "New";
            if (accessibleLabel.IndexOf("Clone this window", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Clone";
            if (accessibleLabel.IndexOf("Open this page", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Open";
            if (accessibleLabel.IndexOf("Windows and recent", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Windows";
            if (accessibleLabel.IndexOf("bookmarks sidebar", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Bookmarks";
            if (accessibleLabel.IndexOf("Add current page", StringComparison.OrdinalIgnoreCase) >= 0)
                return "+ Bookmark";
            if (accessibleLabel.IndexOf("tray shortcut", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Tray";
            if (accessibleLabel.IndexOf("Downloads", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Downloads";
            if (accessibleLabel.IndexOf("Recent conversations", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Recent";
            if (accessibleLabel.IndexOf("Capture full page", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Capture";
            if (accessibleLabel.IndexOf("Check for updates", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Update";
            if (accessibleLabel.IndexOf("Exit completely", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Exit";

            int space = accessibleLabel.IndexOf(' ');
            return space >= 0 && space + 1 < accessibleLabel.Length
                ? accessibleLabel.Substring(space + 1).Trim()
                : accessibleLabel.Trim();
        }

        private static bool ShouldOpenExternally(Uri uri)
        {
            string host = uri.Host.ToLowerInvariant();
            if (host == "chatgpt.com" || host.EndsWith(".chatgpt.com") ||
                host == "openai.com" || host.EndsWith(".openai.com"))
                return false;

            string combined = host + uri.AbsolutePath.ToLowerInvariant();
            if (combined.Contains("auth") || combined.Contains("login") || combined.Contains("account") ||
                host.Contains("google") || host.Contains("microsoftonline") || host.Contains("live.com") ||
                host.Contains("apple.com") || host.Contains("auth0"))
                return false;
            return true;
        }

        private static string NormalizeNavigationUrl(string value)
        {
            string text = (value ?? string.Empty).Trim();
            if (text.Length == 0)
                return AppController.HomeUrl;

            Uri uri;
            if (Uri.TryCreate(text, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "about"))
                return uri.AbsoluteUri;

            if (!text.Contains("://") && Uri.TryCreate("https://" + text, UriKind.Absolute, out uri))
                return uri.AbsoluteUri;

            return AppController.HomeUrl;
        }

        private static void TryOpenExternal(string url)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(url))
                    Process.Start(url);
            }
            catch
            {
            }
        }
    }
}
