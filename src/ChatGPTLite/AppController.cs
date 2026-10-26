using Microsoft.Web.WebView2.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace ChatGPTLite
{
    internal sealed class AppController : ApplicationContext
    {
        public const string HomeUrl = "https://chatgpt.com/";
        public const string ProductName = "RPM Browser Lite";
        public const string ProductVersion = "2.8.26";
        public const string DefaultUpdateManifestUrl = "https://api.github.com/repos/rodrigopavezmina/rpm-browser-lite/contents/update-manifest.json?ref=main";
        private const string LegacyUpdateManifestUrl = "https://raw.githubusercontent.com/rodrigopavezmina/rpm-browser-lite/main/update-manifest.json";

        private readonly AppPaths _paths;
        private readonly string _pipeName;
        private readonly Dictionary<string, BrowserWindow> _windows;
        private readonly Dictionary<string, NotifyIcon> _trayShortcuts;
        private readonly Dictionary<string, Icon> _trayShortcutAssets;
        private readonly Control _dispatcher;
        private readonly NotifyIcon _trayIcon;
        private readonly System.Windows.Forms.Timer _saveTimer;
        private readonly AltTriplePressHook _altTriplePressHook;
        private readonly System.Windows.Forms.Timer _libraryRefreshTimer;
        private Task<CoreWebView2Environment> _environmentTask;
        private LibraryForm _library;
        private SettingsForm _settings;
        private bool _exiting;
        private bool _bookmarkBatch;
        private bool _bulkHibernateRunning;
        private bool _defaultUpdateManifestApplied;

        public AppState State { get; private set; }
        public AppPaths Paths { get { return _paths; } }

        public AppController(AppPaths paths, string pipeName, string initialUrl)
        {
            _paths = paths;
            _pipeName = pipeName;
            _windows = new Dictionary<string, BrowserWindow>(StringComparer.OrdinalIgnoreCase);
            _trayShortcuts = new Dictionary<string, NotifyIcon>(StringComparer.OrdinalIgnoreCase);
            _trayShortcutAssets = new Dictionary<string, Icon>(StringComparer.OrdinalIgnoreCase);

            try
            {
                State = JsonStore.Load<AppState>(_paths.StateFile) ?? AppState.CreateDefault();
                State.RepairDefaults();
                // Existing installations created before the built-in update channel
                // had an empty manifest URL. Keep their opt-in automatic updates
                // working without requiring a manual configuration step.
                if (State.Settings.AutomaticUpdates == true &&
                    (string.IsNullOrWhiteSpace(State.Settings.UpdateManifestUrl) ||
                     string.Equals(State.Settings.UpdateManifestUrl.Trim(), LegacyUpdateManifestUrl, StringComparison.OrdinalIgnoreCase)))
                {
                    State.Settings.UpdateManifestUrl = DefaultUpdateManifestUrl;
                    _defaultUpdateManifestApplied = true;
                }
            }
            catch (Exception ex)
            {
                Log("No se pudo leer el estado local", ex);
                State = AppState.CreateDefault();
            }

            _dispatcher = new Control();
            IntPtr unusedHandle = _dispatcher.Handle;

            _saveTimer = new System.Windows.Forms.Timer();
            _saveTimer.Interval = 900;
            _saveTimer.Tick += delegate
            {
                _saveTimer.Stop();
                SaveNow();
            };
            if (_defaultUpdateManifestApplied)
                QueueSave();

            _libraryRefreshTimer = new System.Windows.Forms.Timer();
            _libraryRefreshTimer.Interval = 250;
            _libraryRefreshTimer.Tick += delegate
            {
                _libraryRefreshTimer.Stop();
                if (_library != null && !_library.IsDisposed)
                    _library.RefreshContent();
            };

            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Font = new Font("Segoe UI", 11f);
            trayMenu.ImageScalingSize = new Size(24, 24);
            trayMenu.Items.Add("✦ Quick Gemini · invisible", null, delegate { OpenQuickConsult(); });
            trayMenu.Items.Add("◈ Quick ChatGPT · private", null, delegate { OpenQuickChatGpt(); });
            trayMenu.Items.Add("◉ Quick Duck.ai · private", null, delegate { OpenQuickDuckAi(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("✚ New window", null, delegate { OpenWindow(HomeUrl, true); });
            trayMenu.Items.Add("▦ Windows and recent", null, delegate { ShowLibrary(); });
            trayMenu.Items.Add("☾ Hibernate inactive windows", null, delegate { HibernateInactiveWindows(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("⚙ Options", null, delegate { ShowSettings(); });
            trayMenu.Items.Add("↓ Check for updates", null, async delegate { await CheckForUpdatesAsync(true); });
            trayMenu.Items.Add("ⓘ About " + ProductName + " " + ProductVersion, null, delegate { ShowAbout(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("× Exit", null, delegate { ExitApplication(); });

            _trayIcon = new NotifyIcon();
            _trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            _trayIcon.Text = ProductName;
            _trayIcon.Visible = true;
            _trayIcon.ContextMenuStrip = trayMenu;
            _trayIcon.MouseDoubleClick += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button == MouseButtons.Left)
                {
                    OpenWindow(HomeUrl, true);
                }
            };
            RefreshTrayShortcuts();

            _altTriplePressHook = new AltTriplePressHook(delegate
            {
                if (!_exiting)
                    _dispatcher.BeginInvoke((MethodInvoker)delegate { if (!_exiting) OpenQuickConsult(); });
            });
            _altTriplePressHook.Start();

            RestoreOrCreateWindows(
                IsInternalCommand(initialUrl) ? null : initialUrl,
                // A normal launch is intentionally manager-only.  The manager
                // lets the user open one or more records deliberately, so a
                // large session never creates a burst of WebView processes.
                string.IsNullOrWhiteSpace(initialUrl) ||
                string.Equals(initialUrl, "@show-manager", StringComparison.Ordinal));
            StartPipeListener();
            if (State.Settings.AutomaticUpdates == true && !string.IsNullOrWhiteSpace(State.Settings.UpdateManifestUrl))
                _dispatcher.BeginInvoke((MethodInvoker)async delegate { await CheckForUpdatesAsync(false); });
        }

        public Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            if (_environmentTask == null)
            {
                int cacheMb = Math.Max(64, State.Settings.CacheSizeMb);
                long cacheBytes = (long)cacheMb * 1024L * 1024L;
                CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions();
                options.AdditionalBrowserArguments =
                    "--autoplay-policy=user-gesture-required " +
                    "--disable-features=OverlayScrollbar,OverlayScrollbars " +
                    "--disk-cache-size=" + cacheBytes.ToString();
                _environmentTask = CoreWebView2Environment.CreateAsync(null, _paths.WebViewDirectory, options);
            }
            return _environmentTask;
        }

        public BrowserWindow OpenWindow(string url, bool activate)
        {
            WindowRecord record = WindowRecord.Create(NormalizeUrl(url));
            ApplyDomainProfile(record);
            CenterNewWindow(record);
            State.Windows.Add(record);
            BrowserWindow window = CreateWindow(record, false);
            if (ShouldShowStartDashboard(record))
                window.PrepareStartDashboard();
            window.Show();
            if (activate)
                window.ActivateAndRestore();
            QueueSave();
            return window;
        }

        public BrowserWindow CloneWindow(BrowserWindow source, bool activate)
        {
            if (source == null || source.IsDisposed || source.Disposing || source.Record == null)
                return null;

            source.UpdateRecordFromWindow();
            WindowRecord original = source.Record;
            WindowRecord copy = WindowRecord.Create(original.Url);
            copy.Title = original.Title;
            copy.CustomTitle = original.CustomTitle;
            copy.Identifier = original.Identifier;
            copy.IconGlyph = original.IconGlyph;
            copy.IconColorArgb = original.IconColorArgb;
            copy.UseFavicon = original.UseFavicon;
            copy.FaviconPath = original.FaviconPath;
            copy.CustomIconPath = original.CustomIconPath;
            copy.TrayShortcut = original.TrayShortcut;
            copy.TraySingleInstance = original.TraySingleInstance;
            copy.Width = original.Width;
            copy.Height = original.Height;
            copy.Maximized = original.Maximized;
            copy.Hibernated = original.Hibernated;
            copy.CaffeineMode = original.CaffeineMode;
            copy.KeepHibernated = original.KeepHibernated;
            copy.NotesVisible = original.NotesVisible;
            copy.BookmarksVisible = original.BookmarksVisible;
            copy.Category = original.Category;
            copy.NoteText = original.NoteText;
            copy.NoteImages = new List<NoteImageRecord>();

            // Keep the clone beside the source without changing its saved dimensions.
            copy.X = original.X + 32;
            copy.Y = original.Y + 32;
            CenterNewWindow(copy);
            State.Windows.Add(copy);

            CopyCloneNoteFiles(original, copy);
            string sourcePreview = GetHibernationPreviewPath(original);
            string copyPreview = GetHibernationPreviewPath(copy);
            CopyFileIfExists(sourcePreview, copyPreview);
            if (File.Exists(copyPreview))
                copy.HibernationPreviewUtc = DateTime.UtcNow;

            BrowserWindow window = CreateWindow(copy, copy.Hibernated || copy.KeepHibernated);
            window.Show();
            if (activate)
                window.ActivateAndRestore();
            QueueSave();
            RefreshLibrary();
            return window;
        }

        private void CopyCloneNoteFiles(WindowRecord source, WindowRecord target)
        {
            try
            {
                string sourceRtf = GetNoteDocumentPath(source);
                string targetRtf = GetNoteDocumentPath(target);
                CopyFileIfExists(sourceRtf, targetRtf);
                foreach (NoteImageRecord image in source.NoteImages ?? new List<NoteImageRecord>())
                {
                    NoteImageRecord copied = new NoteImageRecord();
                    copied.Id = Guid.NewGuid().ToString("N");
                    copied.FileName = image.FileName;
                    copied.OriginalFileName = image.OriginalFileName;
                    copied.AddedUtc = image.AddedUtc;
                    target.NoteImages.Add(copied);
                    CopyFileIfExists(GetNoteImagePath(source, image), GetNoteImagePath(target, copied));
                }
            }
            catch (Exception ex)
            {
                Log("No se pudieron copiar los archivos de notas al clonar", ex);
            }
        }

        private static void CopyFileIfExists(string source, string target)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target) || !File.Exists(source))
                return;
            string folder = Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);
            File.Copy(source, target, true);
        }

        public string GetFaviconPath(WindowRecord record)
        {
            if (record == null)
                return string.Empty;
            Directory.CreateDirectory(_paths.IconsDirectory);
            return Path.Combine(_paths.IconsDirectory, record.Id + "-favicon.png");
        }

        public string GetSiteFaviconPath(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return string.Empty;
            string origin = (uri.Scheme + "://" + uri.Authority).ToLowerInvariant();
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(origin));
                string token = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant().Substring(0, 20);
                Directory.CreateDirectory(_paths.IconsDirectory);
                return Path.Combine(_paths.IconsDirectory, "site-" + token + ".png");
            }
        }

        public void ApplyDomainProfile(WindowRecord record)
        {
            if (record == null || record.IsQuickConsult)
                return;
            string domain = GetDomainKey(record.Url);
            if (string.IsNullOrWhiteSpace(domain))
                return;
            DomainProfile profile = State.DomainProfiles.FirstOrDefault(
                delegate(DomainProfile item) { return item != null && string.Equals(item.Domain, domain, StringComparison.OrdinalIgnoreCase); });
            if (profile == null)
                return;
            if (!string.IsNullOrWhiteSpace(profile.IconGlyph))
                record.IconGlyph = profile.IconGlyph;
            if ((profile.IconColorArgb & unchecked((int)0xFF000000)) != 0)
                record.IconColorArgb = profile.IconColorArgb;
        }

        public void RememberDomainProfile(WindowRecord record)
        {
            if (record == null || record.IsQuickConsult)
                return;
            string domain = GetDomainKey(record.Url);
            if (string.IsNullOrWhiteSpace(domain))
                return;
            DomainProfile profile = State.DomainProfiles.FirstOrDefault(
                delegate(DomainProfile item) { return item != null && string.Equals(item.Domain, domain, StringComparison.OrdinalIgnoreCase); });
            if (profile == null)
            {
                profile = new DomainProfile();
                profile.Domain = domain;
                State.DomainProfiles.Add(profile);
            }
            profile.IconGlyph = record.IconGlyph;
            profile.IconColorArgb = record.IconColorArgb;
            State.DomainProfiles = State.DomainProfiles
                .OrderBy(delegate(DomainProfile item) { return item.Domain; })
                .ToList();
            QueueSave();
        }

        private static string GetDomainKey(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return string.Empty;
            string host = (uri.Host ?? string.Empty).Trim().ToLowerInvariant();
            return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host.Substring(4) : host;
        }

        public void ToggleTrayShortcut(WindowRecord record)
        {
            if (record == null || record.IsQuickConsult)
                return;
            record.TrayShortcut = !record.TrayShortcut;
            RefreshTrayShortcuts();
            QueueSave();
            RefreshLibrary();
        }

        public void ConfigureTrayShortcut(WindowRecord record)
        {
            if (record == null || record.IsQuickConsult)
                return;

            BrowserWindow window;
            if (!_windows.TryGetValue(record.Id, out window) || window == null || window.IsDisposed)
            {
                // Editing a favorite should not start a WebView process.  A
                // hibernated native shell is enough for the customization form.
                RepairWindowRecord(record);
                window = CreateWindow(record, true);
                window.Show();
            }
            window.ShowCustomization();
            RefreshTrayShortcuts();
        }

        public void ToggleTraySingleInstance(WindowRecord record)
        {
            if (record == null || record.IsQuickConsult || !record.TrayShortcut)
                return;
            record.TraySingleInstance = !record.TraySingleInstance;
            NotifyIcon shortcut;
            if (_trayShortcuts.TryGetValue(record.Id, out shortcut) && shortcut != null)
            {
                if (shortcut.ContextMenuStrip != null)
                    shortcut.ContextMenuStrip.Dispose();
                shortcut.ContextMenuStrip = CreateTrayShortcutMenu(record);
            }
            RefreshTrayShortcuts();
            QueueSave();
        }

        private void RefreshTrayShortcuts()
        {
            if (_exiting || _trayShortcuts == null)
                return;
            HashSet<string> wanted = new HashSet<string>(State.Windows
                .Where(delegate(WindowRecord item)
                {
                    return item != null && !string.IsNullOrWhiteSpace(item.Id) && item.TrayShortcut && !item.IsQuickConsult && !string.IsNullOrWhiteSpace(item.Url);
                })
                .Select(delegate(WindowRecord item) { return item.Id; }), StringComparer.OrdinalIgnoreCase);

            foreach (string id in _trayShortcuts.Keys.Where(delegate(string key) { return !wanted.Contains(key); }).ToArray())
                RemoveTrayShortcut(id);

            foreach (WindowRecord record in State.Windows.Where(delegate(WindowRecord item)
            {
                return item != null && wanted.Contains(item.Id) && !_trayShortcuts.ContainsKey(item.Id);
            }))
                AddTrayShortcut(record);

            foreach (WindowRecord record in State.Windows.Where(delegate(WindowRecord item)
            {
                return item != null && wanted.Contains(item.Id) && _trayShortcuts.ContainsKey(item.Id);
            }))
                UpdateTrayShortcutIcon(record);
        }

        private void AddTrayShortcut(WindowRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.Id) || _trayShortcuts.ContainsKey(record.Id))
                return;
            string sharedFavicon = GetSiteFaviconPath(record.Url);
            if (record.UseFavicon == true && string.IsNullOrWhiteSpace(record.FaviconPath) && File.Exists(sharedFavicon))
                record.FaviconPath = sharedFavicon;
            Icon asset = IconVisuals.CreateWindowIcon(record);
            NotifyIcon shortcut = new NotifyIcon();
            shortcut.Icon = asset;
            shortcut.Text = TrayShortcutText(record);
            shortcut.Visible = true;
            shortcut.ContextMenuStrip = CreateTrayShortcutMenu(record);
            WindowRecord captured = record;
            shortcut.MouseDoubleClick += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button == MouseButtons.Left && !_exiting)
                {
                    if (captured.TraySingleInstance)
                    {
                        BrowserWindow existing = GetOpenWindow(captured.Id);
                        if (existing != null && !existing.IsDisposed && !existing.Disposing)
                            existing.ActivateAndRestore();
                        else
                            OpenSavedWindow(captured.Id, true);
                    }
                    else
                        OpenWindow(captured.Url, true);
                }
            };
            _trayShortcuts[record.Id] = shortcut;
            _trayShortcutAssets[record.Id] = asset;
        }

        private void UpdateTrayShortcutIcon(WindowRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.Id))
                return;
            NotifyIcon shortcut;
            if (!_trayShortcuts.TryGetValue(record.Id, out shortcut))
                return;
            string sharedFavicon = GetSiteFaviconPath(record.Url);
            if (record.UseFavicon == true && string.IsNullOrWhiteSpace(record.FaviconPath) && File.Exists(sharedFavicon))
                record.FaviconPath = sharedFavicon;
            Icon next = IconVisuals.CreateWindowIcon(record);
            Icon previous;
            if (_trayShortcutAssets.TryGetValue(record.Id, out previous))
                _trayShortcutAssets[record.Id] = next;
            else
                _trayShortcutAssets.Add(record.Id, next);
            shortcut.Icon = next;
            shortcut.Text = TrayShortcutText(record);
            if (previous != null)
                previous.Dispose();
        }

        private ContextMenuStrip CreateTrayShortcutMenu(WindowRecord record)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 10f);
            WindowRecord captured = record;
            ToolStripMenuItem openItem = new ToolStripMenuItem(
                captured.TraySingleInstance ? "↗ Open existing window" : "✚ Open new window");
            openItem.Click += delegate
            {
                if (captured.TraySingleInstance)
                {
                    BrowserWindow existing = GetOpenWindow(captured.Id);
                    if (existing != null && !existing.IsDisposed && !existing.Disposing)
                        existing.ActivateAndRestore();
                    else
                        OpenSavedWindow(captured.Id, true);
                }
                else
                    OpenWindow(captured.Url, true);
            };
            menu.Items.Add(openItem);
            menu.Items.Add("▦ Open windows and bookmarks", null, delegate { ShowLibrary(); });
            menu.Items.Add("⚙ Configure title, ID and icon", null, delegate { ConfigureTrayShortcut(captured); });
            ToolStripMenuItem singleInstance = new ToolStripMenuItem("✓ Only one instance for this page");
            singleInstance.Checked = captured.TraySingleInstance;
            singleInstance.Click += delegate { ToggleTraySingleInstance(captured); };
            menu.Items.Add(singleInstance);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("× Remove this tray icon", null, delegate { if (captured.TrayShortcut) ToggleTrayShortcut(captured); });
            return menu;
        }

        private void RemoveTrayShortcut(string id)
        {
            NotifyIcon shortcut;
            if (_trayShortcuts.TryGetValue(id, out shortcut))
            {
                shortcut.Visible = false;
                if (shortcut.ContextMenuStrip != null)
                    shortcut.ContextMenuStrip.Dispose();
                shortcut.Dispose();
                _trayShortcuts.Remove(id);
            }
            Icon asset;
            if (_trayShortcutAssets.TryGetValue(id, out asset))
            {
                asset.Dispose();
                _trayShortcutAssets.Remove(id);
            }
        }

        private static string TrayShortcutText(WindowRecord record)
        {
            string title = record == null ? string.Empty : record.DisplayTitle(string.Empty);
            if (string.IsNullOrWhiteSpace(title))
                title = "Favorite page";
            return title.Length > 60 ? title.Substring(0, 57) + "..." : title;
        }

        public string StoreCustomIcon(WindowRecord record, string sourcePath)
        {
            if (record == null || string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return string.Empty;

            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (extension != ".ico" && extension != ".png")
                throw new InvalidDataException("Selecciona un archivo ICO o PNG.");

            Directory.CreateDirectory(_paths.IconsDirectory);
            string target = Path.Combine(_paths.IconsDirectory, record.Id + "-custom.png");
            using (Image source = Image.FromFile(sourcePath))
            using (Bitmap copy = new Bitmap(source))
                copy.Save(target, System.Drawing.Imaging.ImageFormat.Png);
            return target;
        }

        public BrowserWindow OpenQuickConsult()
        {
            return OpenQuickConsult(State.Settings.QuickConsultUrl, "Quick Gemini");
        }

        public BrowserWindow OpenQuickChatGpt()
        {
            return OpenQuickConsult(HomeUrl, "Quick ChatGPT");
        }

        public BrowserWindow OpenQuickDuckAi()
        {
            return OpenQuickConsult("https://duck.ai/chat?q=DuckDuckGo+AI+Chat&ia=chat&duckai=1", "Quick Duck.ai");
        }

        private BrowserWindow OpenQuickConsult(string url, string title)
        {
            WindowRecord record = WindowRecord.Create(NormalizeUrl(url));
            record.IsQuickConsult = true;
            record.CustomTitle = title;
            record.Width = 700;
            record.Height = 540;
            record.Maximized = false;
            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            record.X = workArea.Left + Math.Max(16, (workArea.Width - record.Width) / 2);
            record.Y = workArea.Top + Math.Max(16, (workArea.Height - record.Height) / 2);

            // The transient record stays out of State.Windows and is never reopened at startup.
            BrowserWindow window = CreateWindow(record, false);
            window.Show();
            window.ActivateAndRestore();
            return window;
        }

        public async Task<BrowserWindow> OpenPopupWindowAsync()
        {
            WindowRecord record = WindowRecord.Create("about:blank");
            CenterNewWindow(record);
            State.Windows.Add(record);
            BrowserWindow window = CreateWindow(record, false);
            window.Show();
            await window.EnsureBrowserReadyAsync(false);
            return window;
        }

        public void OnWindowClosed(BrowserWindow window, bool preserveRecord)
        {
            if (window == null)
                return;

            _windows.Remove(window.Record.Id);
            if (!_exiting && !preserveRecord && !window.Record.TrayShortcut)
            {
                State.Windows.Remove(window.Record);
                QueueSave();
            }
            else if (!_exiting && window.Record.TrayShortcut)
            {
                RefreshTrayShortcuts();
                QueueSave();
            }
            RefreshLibrary();
            if (!_exiting && _windows.Count == 0 && !State.Settings.MinimizeToTray)
                ExitApplication();
        }

        public void MoveWindowsToRecycle(IEnumerable<BrowserWindow> windows)
        {
            if (windows == null)
                return;
            foreach (BrowserWindow window in windows.Where(delegate(BrowserWindow item) { return item != null; }).Distinct().ToArray())
            {
                window.UpdateRecordFromWindow();
                if (!State.Windows.Remove(window.Record))
                    continue;
                DeletedWindowRecord deleted = new DeletedWindowRecord();
                deleted.Record = window.Record;
                deleted.DeletedUtc = DateTime.UtcNow;
                State.RecycleBin.Add(deleted);
                window.CloseForRecycleBin();
            }
            QueueSave();
            RefreshLibrary();
        }

        public void RestoreRecycledWindows(IEnumerable<DeletedWindowRecord> deletedWindows)
        {
            if (deletedWindows == null)
                return;
            BrowserWindow first = null;
            foreach (DeletedWindowRecord deleted in deletedWindows.Where(delegate(DeletedWindowRecord item) { return item != null && item.Record != null; }).ToArray())
            {
                WindowRecord record = deleted.Record;
                if (State.Windows.Any(delegate(WindowRecord existing) { return existing.Id == record.Id; }))
                    continue;
                RepairWindowRecord(record);
                State.Windows.Add(record);
                BrowserWindow window = CreateWindow(record, record.Hibernated || record.KeepHibernated);
                window.Show();
                State.RecycleBin.Remove(deleted);
                if (first == null)
                    first = window;
            }
            if (first != null)
                first.ActivateAndRestore();
            QueueSave();
            RefreshLibrary();
        }

        public void EmptyRecycleBin()
        {
            State.RecycleBin.Clear();
            QueueSave();
            RefreshLibrary();
        }

        public void TrackRecent(string url, string title)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
                return;
            if (!LooksLikeUsefulPage(uri))
                return;

            RecentRecord existing = State.Recents.FirstOrDefault(
                delegate(RecentRecord item) { return string.Equals(item.Url, url, StringComparison.OrdinalIgnoreCase); });
            DateTime now = DateTime.UtcNow;
            bool countVisit = existing == null || existing.LastOpenedUtc < now.AddSeconds(-4);
            if (existing == null)
            {
                existing = new RecentRecord();
                existing.Url = url;
                State.Recents.Add(existing);
            }

            existing.Title = CleanTitle(title);
            if (countVisit)
                existing.VisitCount = Math.Max(0, existing.VisitCount) + 1;
            existing.LastOpenedUtc = now;
            State.Recents = State.Recents
                .OrderByDescending(delegate(RecentRecord item) { return item.LastOpenedUtc; })
                .Take(100)
                .ToList();
            QueueSave();
            RefreshLibrary();
        }

        public IList<RecentRecord> GetMostUsedStartPages(int maximum)
        {
            return State.Recents
                .Where(delegate(RecentRecord item) { return item != null && !string.IsNullOrWhiteSpace(item.Url); })
                .OrderByDescending(delegate(RecentRecord item) { return item.VisitCount; })
                .ThenByDescending(delegate(RecentRecord item) { return item.LastOpenedUtc; })
                .Take(Math.Max(1, maximum))
                .ToList();
        }

        public IList<RecentRecord> GetRecentStartPages(int maximum, IEnumerable<string> excludedUrls)
        {
            HashSet<string> excluded = new HashSet<string>(excludedUrls ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return State.Recents
                .Where(delegate(RecentRecord item)
                {
                    return item != null && !string.IsNullOrWhiteSpace(item.Url) && !excluded.Contains(item.Url);
                })
                .OrderByDescending(delegate(RecentRecord item) { return item.LastOpenedUtc; })
                .Take(Math.Max(1, maximum))
                .ToList();
        }

        public IList<BrowserWindow> OpenWindows
        {
            get { return _windows.Values.Where(delegate(BrowserWindow w) { return w != null && !w.IsDisposed && !w.Disposing; }).OrderByDescending(delegate(BrowserWindow w) { return w.Record.LastActiveUtc; }).ToList(); }
        }

        // Saved records are deliberately kept separate from live forms.  A record can
        // remain in the manager without creating a native window or a WebView2 process.
        public IList<WindowRecord> SavedWindows
        {
            get { return State.Windows.OrderByDescending(delegate(WindowRecord record) { return record.LastActiveUtc; }).ToList(); }
        }

        public BrowserWindow GetOpenWindow(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;
            BrowserWindow window;
            return _windows.TryGetValue(id, out window) ? window : null;
        }

        public BrowserWindow OpenSavedWindow(string id, bool activate)
        {
            WindowRecord record = State.Windows.FirstOrDefault(
                delegate(WindowRecord item) { return string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase); });
            if (record == null)
                return null;

            BrowserWindow existing = GetOpenWindow(record.Id);
            if (existing != null && !existing.IsDisposed && !existing.Disposing)
            {
                if (activate)
                    existing.ActivateAndRestore();
                return existing;
            }
            if (existing != null)
                _windows.Remove(record.Id);

            RepairWindowRecord(record);
            BrowserWindow window = CreateWindow(record, record.Hibernated || record.KeepHibernated);
            window.Show();
            if (activate)
                window.ActivateAndRestore();
            QueueSave();
            return window;
        }

        public void MarkSavedWindowsAsHibernated(IEnumerable<WindowRecord> records)
        {
            if (records == null)
                return;
            foreach (WindowRecord record in records.Where(delegate(WindowRecord item) { return item != null; }).ToArray())
            {
                BrowserWindow live = GetOpenWindow(record.Id);
                if (live != null)
                    live.Hibernate();
                else
                {
                    record.Hibernated = true;
                    record.CaffeineMode = false;
                    record.KeepHibernated = false;
                }
            }
            QueueSave();
            RefreshLibrary();
        }

        public void ToggleSavedCaffeine(IEnumerable<WindowRecord> records)
        {
            if (records == null)
                return;
            foreach (WindowRecord record in records.Where(delegate(WindowRecord item) { return item != null; }).ToArray())
            {
                BrowserWindow live = GetOpenWindow(record.Id);
                if (live != null)
                    live.ToggleCaffeine();
                else
                {
                    record.CaffeineMode = !record.CaffeineMode;
                    if (record.CaffeineMode)
                    {
                        record.Hibernated = false;
                        record.KeepHibernated = false;
                    }
                }
            }
            QueueSave();
            RefreshLibrary();
        }

        public void ToggleSavedPermanentHibernate(IEnumerable<WindowRecord> records)
        {
            if (records == null)
                return;
            foreach (WindowRecord record in records.Where(delegate(WindowRecord item) { return item != null; }).ToArray())
            {
                BrowserWindow live = GetOpenWindow(record.Id);
                if (live != null)
                    live.TogglePermanentHibernate();
                else
                {
                    record.KeepHibernated = !record.KeepHibernated;
                    if (record.KeepHibernated)
                    {
                        record.CaffeineMode = false;
                        record.Hibernated = true;
                    }
                }
            }
            QueueSave();
            RefreshLibrary();
        }

        public void MoveSavedWindowsToRecycle(IEnumerable<WindowRecord> records)
        {
            if (records == null)
                return;
            foreach (WindowRecord selected in records.Where(delegate(WindowRecord item) { return item != null; }).Distinct().ToArray())
            {
                WindowRecord record = State.Windows.FirstOrDefault(
                    delegate(WindowRecord item) { return string.Equals(item.Id, selected.Id, StringComparison.OrdinalIgnoreCase); });
                if (record == null)
                    continue;
                BrowserWindow live = GetOpenWindow(record.Id);
                if (live != null)
                    live.UpdateRecordFromWindow();
                State.Windows.Remove(record);
                DeletedWindowRecord deleted = new DeletedWindowRecord();
                deleted.Record = record;
                deleted.DeletedUtc = DateTime.UtcNow;
                State.RecycleBin.Add(deleted);
                if (live != null)
                    live.CloseForRecycleBin();
            }
            QueueSave();
            RefreshLibrary();
        }

        public void ShowLibrary()
        {
            if (_library == null || _library.IsDisposed)
            {
                _library = new LibraryForm(this);
                _library.FormClosed += delegate { _library = null; };
            }
            _library.RefreshContent();
            _library.Show();
            if (_library.WindowState == FormWindowState.Minimized)
                _library.WindowState = FormWindowState.Normal;
            _library.Activate();
            _library.FocusSearch();
        }

        public void ShowSettings()
        {
            if (_settings == null || _settings.IsDisposed)
            {
                _settings = new SettingsForm(this);
                _settings.FormClosed += delegate { _settings = null; };
            }
            _settings.Show();
            if (_settings.WindowState == FormWindowState.Minimized)
                _settings.WindowState = FormWindowState.Normal;
            _settings.Activate();
        }

        public void SettingsChanged()
        {
            QueueSave();
            ApplyThemeToOpenWindows();
        }

        public void PreviewDarkMode(bool dark)
        {
            State.Settings.DarkMode = dark;
            ApplyThemeToOpenWindows();
        }

        private void ApplyThemeToOpenWindows()
        {
            foreach (BrowserWindow window in _windows.Values.Where(delegate(BrowserWindow item) { return item != null && !item.IsDisposed && !item.Disposing; }))
            {
                window.ApplyPresentation();
                window.RefreshResourceTimer();
            }
            if (_library != null && !_library.IsDisposed)
                _library.ApplyTheme();
        }

        public void ShowAbout()
        {
            MessageBox.Show(
                ProductName + "\r\nVersion " + ProductVersion +
                "\r\n\r\nA lightweight Windows browser shell with suspend, hibernation, local previews and a shared WebView2 profile.\r\nThe dark option applies to the native interface; each website controls its own appearance.",
                "About " + ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        public void RememberIdentifier(string identifier)
        {
            string value = (identifier ?? string.Empty).Trim();
            if (value.Length == 0)
                return;

            State.KnownIdentifiers.RemoveAll(delegate(string known)
            {
                return string.Equals(known, value, StringComparison.OrdinalIgnoreCase);
            });
            State.KnownIdentifiers.Add(value);
            State.KnownIdentifiers = State.KnownIdentifiers
                .OrderBy(delegate(string known) { return known; })
                .Take(100)
                .ToList();
        }

        public void WindowPresentationChanged()
        {
            QueueSave();
            RefreshTrayShortcuts();
            RefreshLibrary();
        }

        public string NormalizeIdentifierInput(string identifier)
        {
            string value = (identifier ?? string.Empty).Trim();
            if (value.Length == 0)
                return string.Empty;

            Match numberMatch = Regex.Match(value, "^(?:TD-D?)?0*(?<number>[0-9]{1,2})$", RegexOptions.IgnoreCase);
            int number;
            if (numberMatch.Success && int.TryParse(numberMatch.Groups["number"].Value, out number) && number >= 1 && number <= 50)
                return "TD-D" + number.ToString("00");
            return value;
        }

        public void RefreshBookmarks()
        {
            QueueSave();
            foreach (BrowserWindow window in _windows.Values.Where(delegate(BrowserWindow item) { return item != null && !item.IsDisposed && !item.Disposing; }))
                window.RefreshBookmarks();
            RefreshLibrary();
        }

        public string GetHibernationPreviewPath(WindowRecord record)
        {
            return GetSafeWindowFilePath(_paths.PreviewsDirectory, record, ".jpg");
        }

        public string GetLatestWindowPreviewPath(WindowRecord record)
        {
            if (record == null)
                return string.Empty;

            List<string> candidates = new List<string>();
            string hibernation = GetHibernationPreviewPath(record);
            if (File.Exists(hibernation))
                candidates.Add(hibernation);

            string token = GetSafeWindowToken(record);
            if (Directory.Exists(_paths.CapturesDirectory))
            {
                string suffix = "_" + token + ".png";
                candidates.AddRange(Directory.GetFiles(_paths.CapturesDirectory, "*.png")
                    .Where(delegate(string path) { return path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase); }));
            }

            return candidates
                .Where(File.Exists)
                .OrderByDescending(delegate(string path) { return File.GetLastWriteTimeUtc(path); })
                .FirstOrDefault() ?? string.Empty;
        }

        public string CreateCapturePath(WindowRecord record)
        {
            string token = GetSafeWindowToken(record);
            Directory.CreateDirectory(_paths.CapturesDirectory);
            return Path.Combine(
                _paths.CapturesDirectory,
                DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + token + ".png");
        }

        public BookmarkRecord AddBookmark(string url, string title)
        {
            return AddBookmark(url, title, string.Empty);
        }

        public BookmarkRecord AddBookmark(string url, string title, string folderPath)
        {
            Uri uri;
            if (!Uri.TryCreate((url ?? string.Empty).Trim(), UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("Enter a valid web address for the bookmark.");
            string normalized = uri.AbsoluteUri;

            BookmarkRecord existing = State.Bookmarks.FirstOrDefault(
                delegate(BookmarkRecord item) { return string.Equals(item.Url, normalized, StringComparison.OrdinalIgnoreCase); });
            if (existing == null)
            {
                existing = new BookmarkRecord();
                existing.Url = normalized;
                existing.CreatedUtc = DateTime.UtcNow;
                State.Bookmarks.Add(existing);
            }
            existing.Title = string.IsNullOrWhiteSpace(title) ? normalized : title.Trim();
            if (!string.IsNullOrWhiteSpace(folderPath))
                existing.FolderPath = NormalizeBookmarkFolder(folderPath);
            else if (existing.FolderPath == null)
                existing.FolderPath = string.Empty;
            QueueSave();
            if (!_bookmarkBatch)
                RefreshBookmarks();
            return existing;
        }

        public void RemoveBookmarks(IEnumerable<BookmarkRecord> bookmarks)
        {
            if (bookmarks == null)
                return;
            foreach (BookmarkRecord bookmark in bookmarks.ToArray())
                State.Bookmarks.Remove(bookmark);
            QueueSave();
            RefreshBookmarks();
        }

        public int ImportBookmarks(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("The bookmark file could not be found.");

            string content = File.ReadAllText(filePath, Encoding.UTF8);
            int added = 0;
            _bookmarkBatch = true;
            try
            {
                foreach (BookmarkImportItem item in ParseBookmarks(content))
                {
                    try
                    {
                        int before = State.Bookmarks.Count;
                        AddBookmark(item.Url, item.Title, item.FolderPath);
                        if (State.Bookmarks.Count > before)
                            added++;
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                _bookmarkBatch = false;
            }
            QueueSave();
            RefreshBookmarks();
            return added;
        }

        public void ExportBookmarks(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new InvalidOperationException("Choose where to save the bookmark file.");
            using (StreamWriter writer = new StreamWriter(filePath, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
                writer.WriteLine("<!-- This file can be imported by Chrome and other browsers. -->");
                writer.WriteLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
                writer.WriteLine("<TITLE>RPM Browser Lite Bookmarks</TITLE>");
                writer.WriteLine("<H1>RPM Browser Lite Bookmarks</H1>");
                writer.WriteLine("<DL><p>");
                WriteBookmarkEntries(writer, State.Bookmarks.Where(delegate(BookmarkRecord item) { return string.IsNullOrWhiteSpace(item.FolderPath); }));
                WriteBookmarkFolders(writer, string.Empty, State.Bookmarks);
                writer.WriteLine("</DL><p>");
            }
        }

        private static void WriteBookmarkEntries(StreamWriter writer, IEnumerable<BookmarkRecord> bookmarks)
        {
            foreach (BookmarkRecord bookmark in bookmarks.OrderBy(delegate(BookmarkRecord item) { return item.Title; }))
            {
                string title = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(bookmark.Title) ? bookmark.Url : bookmark.Title);
                string url = WebUtility.HtmlEncode(bookmark.Url ?? string.Empty);
                writer.WriteLine("    <DT><A HREF=\"" + url + "\">" + title + "</A>");
            }
        }

        private static void WriteBookmarkFolders(StreamWriter writer, string parent, IList<BookmarkRecord> allBookmarks)
        {
            string prefix = string.IsNullOrWhiteSpace(parent) ? string.Empty : parent + "\\";
            string[] folders = allBookmarks
                .Select(delegate(BookmarkRecord item) { return item.FolderPath ?? string.Empty; })
                .Where(delegate(string path) { return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && path.Length > prefix.Length; })
                .Select(delegate(string path) { return path.Substring(prefix.Length).Split(new char[] { '\\' }, 2)[0]; })
                .Where(delegate(string name) { return name.Length > 0; })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(delegate(string name) { return name; })
                .ToArray();
            foreach (string folder in folders)
            {
                string fullPath = prefix + folder;
                writer.WriteLine("    <DT><H3>" + WebUtility.HtmlEncode(folder) + "</H3>");
                writer.WriteLine("    <DL><p>");
                WriteBookmarkEntries(writer, allBookmarks.Where(delegate(BookmarkRecord item) { return string.Equals(item.FolderPath ?? string.Empty, fullPath, StringComparison.OrdinalIgnoreCase); }));
                WriteBookmarkFolders(writer, fullPath, allBookmarks);
                writer.WriteLine("    </DL><p>");
            }
        }

        public bool NoteDocumentExists(WindowRecord record)
        {
            return record != null && File.Exists(GetNoteDocumentPath(record));
        }

        public bool LoadNoteDocument(WindowRecord record, RichTextBox editor)
        {
            if (record == null || editor == null)
                return false;

            string path = GetNoteDocumentPath(record);
            if (File.Exists(path))
            {
                try
                {
                    editor.LoadFile(path, RichTextBoxStreamType.RichText);
                    return true;
                }
                catch (Exception ex)
                {
                    Log("No se pudo abrir una nota RTF", ex);
                }
            }

            editor.Text = record.NoteText ?? string.Empty;
            return false;
        }

        public bool SaveNoteDocument(WindowRecord record, RichTextBox editor)
        {
            if (record == null || editor == null)
                return false;

            string path = GetNoteDocumentPath(record);
            string temporaryPath = path + ".saving";
            Directory.CreateDirectory(_paths.NotesDirectory);
            try
            {
                editor.SaveFile(temporaryPath, RichTextBoxStreamType.RichText);
                File.Copy(temporaryPath, path, true);
                File.Delete(temporaryPath);
                record.NoteText = editor.Text ?? string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                Log("No se pudo guardar una nota RTF", ex);
                return false;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        public NoteImageRecord ImportNoteImage(WindowRecord record, string sourcePath)
        {
            if (record == null || string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("No se encontró la imagen que intentas adjuntar.");
            record.RepairPresentationDefaults();
            if (record.NoteImages.Count >= 30)
                throw new InvalidOperationException("Cada nota admite hasta 30 imágenes adjuntas.");

            FileInfo source = new FileInfo(sourcePath);
            if (source.Length > 20L * 1024L * 1024L)
                throw new InvalidOperationException("Cada imagen puede pesar hasta 20 MB.");

            string extension = source.Extension.ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".gif" && extension != ".bmp")
                throw new InvalidOperationException("Puedes adjuntar imágenes PNG, JPG, GIF o BMP.");

            using (Image validation = Image.FromFile(source.FullName))
            {
                if ((long)validation.Width * validation.Height > 36000000L)
                    throw new InvalidOperationException("La resolución máxima por imagen es de 36 megapíxeles.");
            }

            NoteImageRecord image = CreateNoteImageRecord(record, extension, source.Name);
            File.Copy(source.FullName, GetNoteImagePath(record, image), false);
            record.NoteImages.Add(image);
            WindowPresentationChanged();
            return image;
        }

        public NoteImageRecord ImportClipboardImage(WindowRecord record, Image sourceImage)
        {
            if (record == null || sourceImage == null)
                throw new InvalidOperationException("No hay una imagen para adjuntar.");
            record.RepairPresentationDefaults();
            if (record.NoteImages.Count >= 30)
                throw new InvalidOperationException("Cada nota admite hasta 30 imágenes adjuntas.");

            if ((long)sourceImage.Width * sourceImage.Height > 36000000L)
                throw new InvalidOperationException("La resolución máxima por imagen es de 36 megapíxeles.");

            NoteImageRecord image = CreateNoteImageRecord(record, ".png", "Imagen pegada.png");
            using (Bitmap copy = new Bitmap(sourceImage))
                copy.Save(GetNoteImagePath(record, image), System.Drawing.Imaging.ImageFormat.Png);
            record.NoteImages.Add(image);
            WindowPresentationChanged();
            return image;
        }

        public string GetNoteImagePath(WindowRecord record, NoteImageRecord image)
        {
            if (record == null || image == null || string.IsNullOrWhiteSpace(image.FileName))
                throw new InvalidOperationException("La referencia de imagen no es válida.");
            string folder = GetNoteFolder(record);
            string name = Path.GetFileName(image.FileName);
            if (!string.Equals(name, image.FileName, StringComparison.Ordinal))
                throw new InvalidOperationException("El nombre de imagen no es válido.");
            return Path.Combine(folder, name);
        }

        public void DeleteNoteImage(WindowRecord record, NoteImageRecord image)
        {
            if (record == null || image == null)
                return;
            string path = GetNoteImagePath(record, image);
            if (File.Exists(path))
                File.Delete(path);
            record.NoteImages.Remove(image);
            WindowPresentationChanged();
        }

        public void ClearRecents()
        {
            State.Recents.Clear();
            QueueSave();
            RefreshLibrary();
        }

        public async void HibernateInactiveWindows()
        {
            try
            {
                await HibernateInactiveWindowsAsync();
            }
            catch (Exception ex)
            {
                Log("No se pudieron hibernar las ventanas inactivas", ex);
            }
        }

        public async Task HibernateInactiveWindowsAsync()
        {
            if (_bulkHibernateRunning)
                return;
            _bulkHibernateRunning = true;
            try
            {
                foreach (BrowserWindow window in _windows.Values.ToArray())
                {
                    if (window == null || window.IsDisposed || window.ContainsFocus || window.IsCaffeinated)
                        continue;
                    // Run expensive full-page preview capture one window at a time so a
                    // slow computer never allocates several large screenshots together.
                    await window.HibernateAsync();
                }
            }
            finally
            {
                _bulkHibernateRunning = false;
                RefreshLibrary();
            }
        }

        public MemorySnapshot GetMemorySnapshot()
        {
            MemorySnapshot result = new MemorySnapshot();
            HashSet<int> processIds = GetChildProcessIds(Process.GetCurrentProcess().Id);
            foreach (int processId in processIds)
            {
                try
                {
                    using (Process process = Process.GetProcessById(processId))
                    {
                        result.ApplicationWorkingSetBytes += process.WorkingSet64;
                        result.ApplicationProcessCount++;
                    }
                }
                catch
                {
                }
            }

            MEMORYSTATUSEX memory = new MEMORYSTATUSEX();
            memory.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref memory))
            {
                result.TotalPhysicalBytes = memory.ullTotalPhys;
                result.AvailablePhysicalBytes = memory.ullAvailPhys;
                result.MemoryLoadPercent = memory.dwMemoryLoad;
            }
            return result;
        }

        public async Task CheckForUpdatesAsync(bool userInitiated)
        {
            string url = State.Settings.UpdateManifestUrl == null
                ? string.Empty
                : State.Settings.UpdateManifestUrl.Trim();
            if (url.Length == 0)
            {
                if (userInitiated)
                {
                    MessageBox.Show(
                        "Todavía no hay un canal de actualizaciones configurado. La aplicación sigue usando automáticamente la versión actual de ChatGPT y el WebView2 actualizado por Windows.",
                        "Actualizaciones",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                return;
            }

            try
            {
                string json;
                using (WebClient client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "RPMBrowserLite/" + ProductVersion;
                    Uri manifestUri = new Uri(url);
                    json = await client.DownloadStringTaskAsync(manifestUri);
                    if (IsOfficialApiManifestUrl(manifestUri))
                    {
                        GitHubContentEnvelope envelope = JsonStore.FromJson<GitHubContentEnvelope>(json);
                        if (envelope == null || string.IsNullOrWhiteSpace(envelope.Content))
                            throw new InvalidDataException("La respuesta del canal de actualizaciones no contiene el manifiesto.");
                        json = Encoding.UTF8.GetString(Convert.FromBase64String(envelope.Content.Replace("\n", string.Empty).Replace("\r", string.Empty)));
                    }
                }
                UpdateManifest manifest = JsonStore.FromJson<UpdateManifest>(json);
                Version current = new Version(ProductVersion);
                Version remote;
                if (manifest == null || !Version.TryParse(manifest.Version, out remote))
                    throw new InvalidDataException("El manifiesto de actualización no es válido.");

                if (remote <= current)
                {
                    if (userInitiated)
                        MessageBox.Show("Ya tienes la versión más reciente.", "Actualizaciones", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (State.Settings.AutomaticUpdates == true && IsVerifiedUpdate(manifest))
                {
                    await DownloadAndInstallUpdateAsync(manifest);
                    return;
                }
                if (!userInitiated)
                    return;

                DialogResult result = MessageBox.Show(
                    "Hay una versión nueva: " + remote + ".\r\n\r\n" + (manifest.Notes ?? string.Empty) + "\r\n\r\n¿Abrir la descarga?",
                    "Actualización disponible",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);
                if (result == DialogResult.Yes && !string.IsNullOrWhiteSpace(manifest.DownloadUrl))
                    Process.Start(manifest.DownloadUrl);
            }
            catch (Exception ex)
            {
                Log("No se pudo buscar actualizaciones", ex);
                if (userInitiated)
                    MessageBox.Show("No se pudo comprobar la actualización.\r\n\r\n" + ex.Message, "Actualizaciones", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static bool IsOfficialApiManifestUrl(Uri uri)
        {
            return uri != null &&
                   string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase) &&
                   uri.AbsolutePath.EndsWith("/repos/rodrigopavezmina/rpm-browser-lite/contents/update-manifest.json", StringComparison.OrdinalIgnoreCase);
        }

        [DataContract]
        private sealed class GitHubContentEnvelope
        {
            [DataMember(Name = "content")]
            public string Content { get; set; }
        }

        public void QueueSave()
        {
            if (_exiting)
                return;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private static bool IsVerifiedUpdate(UpdateManifest manifest)
        {
            Uri uri;
            return manifest != null &&
                   Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out uri) &&
                   uri.Scheme == Uri.UriSchemeHttps &&
                   Regex.IsMatch(manifest.Sha256 ?? string.Empty, "^[0-9a-fA-F]{64}$");
        }

        private async Task DownloadAndInstallUpdateAsync(UpdateManifest manifest)
        {
            string safeVersion = Regex.Replace(manifest.Version ?? ProductVersion, "[^0-9A-Za-z.-]", string.Empty);
            string installerPath = Path.Combine(Path.GetTempPath(), "RPMBrowserLite-update-" + safeVersion + ".exe");
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "RPMBrowserLite/" + ProductVersion;
                    await client.DownloadFileTaskAsync(new Uri(manifest.DownloadUrl), installerPath);
                }
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(installerPath))
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
                    if (!string.Equals(actual, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The downloaded update failed its SHA-256 check.");
                }
                Process.Start(installerPath, "/silent");
                ExitApplication();
            }
            catch (Exception ex)
            {
                Log("No se pudo instalar automáticamente la actualización", ex);
                try { if (File.Exists(installerPath)) File.Delete(installerPath); }
                catch { }
                if (Environment.UserInteractive)
                    MessageBox.Show("The automatic update could not be installed." + Environment.NewLine + Environment.NewLine + ex.Message, ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void SaveNow()
        {
            try
            {
            foreach (BrowserWindow window in _windows.Values.Where(delegate(BrowserWindow item) { return item != null && !item.IsDisposed && !item.Disposing; }))
                window.UpdateRecordFromWindow();
                JsonStore.Save(_paths.StateFile, State);
            }
            catch (Exception ex)
            {
                Log("No se pudo guardar el estado", ex);
            }
        }

        public void ExitApplication()
        {
            if (_exiting)
                return;
            _exiting = true;
            _saveTimer.Stop();
            _altTriplePressHook.Dispose();
            _libraryRefreshTimer.Stop();
            _libraryRefreshTimer.Dispose();
            SaveNow();

            foreach (BrowserWindow window in _windows.Values.ToArray())
                window.CloseForApplicationExit();

            if (_library != null && !_library.IsDisposed)
                _library.Close();
            if (_settings != null && !_settings.IsDisposed)
                _settings.Close();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            foreach (string id in _trayShortcuts.Keys.ToArray())
                RemoveTrayShortcut(id);
            _dispatcher.Dispose();
            ExitThread();
        }

        public void Log(string message, Exception ex)
        {
            try
            {
                if (File.Exists(_paths.LogFile) && new FileInfo(_paths.LogFile).Length > 4L * 1024L * 1024L)
                {
                    string archive = _paths.LogFile + ".old";
                    File.Copy(_paths.LogFile, archive, true);
                    File.Delete(_paths.LogFile);
                }
                File.AppendAllText(
                    _paths.LogFile,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + "\r\n" +
                    (ex == null ? string.Empty : ex.ToString()) + "\r\n\r\n",
                    Encoding.UTF8);
            }
            catch
            {
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_exiting)
                ExitApplication();
            base.Dispose(disposing);
        }

        private void RestoreOrCreateWindows(string initialUrl, bool managerOnly)
        {
            if (!string.IsNullOrWhiteSpace(initialUrl))
            {
                OpenWindow(initialUrl, true);
                return;
            }

            if (managerOnly)
            {
                // Explicit manager launches must remain safe even with a large saved
                // session: show the records without constructing browser windows or
                // displaying the restore-all prompt.
                ShowLibrary();
                return;
            }

            if (State.Windows.Count == 0)
            {
                OpenWindow(HomeUrl, true);
                return;
            }

            // The setting disables the prompt, never the saved session itself.
            // Keeping the records is what makes it possible to restore them later
            // from the manager without creating any browser windows at startup.
            if (!State.Settings.RestoreWindows)
            {
                ShowLibrary();
                return;
            }

            List<WindowRecord> records = State.Windows
                .OrderBy(delegate(WindowRecord record) { return record.LastActiveUtc; })
                .ToList();
            // Ask on every fresh launch.  The answer is intentionally not persisted:
            // restoring a large session is an explicit, one-time choice and the safe
            // default is to keep every saved window on disk until the user opens it.
            DialogResult answer = MessageBox.Show(
                "Se encontraron " + records.Count + " ventanas guardadas de la sesión anterior." + Environment.NewLine + Environment.NewLine +
                "¿Quieres restaurarlas ahora?" + Environment.NewLine +
                "Sí: restaura las ventanas guardadas (las hibernadas mantienen su captura y no cargan el navegador)." + Environment.NewLine +
                "No: abre solo el administrador; no se crearán ventanas ni procesos web hasta que abras una.",
                ProductName + " · Restaurar sesión",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            bool restoreAll = answer == DialogResult.Yes;
            State.Settings.RestoreAllOnStart = null;
            QueueSave();
            WindowRecord newest = records.LastOrDefault();
            if (!restoreAll)
            {
                // Do not even construct hidden BrowserWindow forms here.  This is the
                // important low-memory path: only the manager and the tray remain alive.
                ShowLibrary();
                return;
            }
            foreach (WindowRecord record in records)
            {
                RepairWindowRecord(record);
                // Keep-awake is a user instruction, so those windows are restored after a restart.
                bool loadNow = record.CaffeineMode || (restoreAll && record == newest && !record.Hibernated);
                BrowserWindow window = CreateWindow(record, !loadNow);
                window.Show();
            }
            if (newest != null && _windows.ContainsKey(newest.Id))
                _windows[newest.Id].ActivateAndRestore();
        }

        private BrowserWindow CreateWindow(WindowRecord record, bool startHibernated)
        {
            ApplyDomainProfile(record);
            BrowserWindow window = new BrowserWindow(this, record, startHibernated);
            _windows[record.Id] = window;
            return window;
        }

        private bool ShouldShowStartDashboard(WindowRecord record)
        {
            if (record == null || record.IsQuickConsult || State.Settings.ShowStartDashboard != true)
                return false;
            return string.Equals(NormalizeUrl(record.Url), HomeUrl, StringComparison.OrdinalIgnoreCase);
        }

        private void CenterNewWindow(WindowRecord record)
        {
            BrowserWindow reference = _windows.Values.FirstOrDefault(
                delegate(BrowserWindow item) { return item.ContainsFocus || Form.ActiveForm == item; });
            if (reference == null)
                reference = _windows.Values.OrderByDescending(delegate(BrowserWindow item) { return item.Record.LastActiveUtc; }).FirstOrDefault();

            Rectangle workArea = reference == null
                ? Screen.PrimaryScreen.WorkingArea
                : Screen.FromControl(reference).WorkingArea;
            Rectangle centerArea = reference != null && reference.WindowState == FormWindowState.Normal
                ? reference.Bounds
                : workArea;

            int safeWidth = Math.Max(560, workArea.Width - 48);
            int safeHeight = Math.Max(460, workArea.Height - 72);
            record.Width = Math.Min(record.Width, safeWidth);
            record.Height = Math.Min(record.Height, safeHeight);

            int centeredX = centerArea.Left + (centerArea.Width - record.Width) / 2;
            int centeredY = centerArea.Top + (centerArea.Height - record.Height) / 2;
            record.X = Math.Max(workArea.Left + 16, Math.Min(centeredX, workArea.Right - record.Width - 16));
            record.Y = Math.Max(workArea.Top + 16, Math.Min(centeredY, workArea.Bottom - record.Height - 16));
        }

        private void RepairWindowRecord(WindowRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.Id))
                record.Id = Guid.NewGuid().ToString("N");
            record.Url = NormalizeUrl(record.Url);
            record.RepairPresentationDefaults();
            if (record.Width < 640)
                record.Width = 1120;
            if (record.Height < 420)
                record.Height = 760;

            Rectangle desired = new Rectangle(record.X, record.Y, record.Width, record.Height);
            bool visible = Screen.AllScreens.Any(delegate(Screen screen) { return screen.WorkingArea.IntersectsWith(desired); });
            if (!visible)
            {
                Rectangle area = Screen.PrimaryScreen.WorkingArea;
                record.X = area.Left + Math.Max(0, (area.Width - record.Width) / 2);
                record.Y = area.Top + Math.Max(0, (area.Height - record.Height) / 2);
            }
        }

        private void StartPipeListener()
        {
            Thread thread = new Thread(delegate()
            {
                while (!_exiting)
                {
                    try
                    {
                        using (NamedPipeServerStream server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None))
                        {
                            server.WaitForConnection();
                            using (StreamReader reader = new StreamReader(server, Encoding.UTF8))
                            {
                                string message = reader.ReadLine();
                                _dispatcher.BeginInvoke((MethodInvoker)delegate
                                {
                                    if (!_exiting)
                                        HandleExternalMessage(message);
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!_exiting)
                            Log("Error en el canal de instancia única", ex);
                    }
                }
            });
            thread.IsBackground = true;
            thread.Name = "ChatGPT Lite IPC";
            thread.Start();
        }

        private void ActivateBestWindow()
        {
            BrowserWindow window = _windows.Values
                .OrderByDescending(delegate(BrowserWindow item) { return item.Record.LastActiveUtc; })
                .FirstOrDefault();
            if (window == null)
                window = OpenWindow(HomeUrl, false);
            window.ActivateAndRestore();
        }

        private void HandleExternalMessage(string message)
        {
            if (string.Equals(message, "@hibernate-all", StringComparison.Ordinal))
            {
                foreach (BrowserWindow window in _windows.Values.ToArray())
                    window.Hibernate();
                return;
            }
            if (string.Equals(message, "@show-manager", StringComparison.Ordinal))
            {
                ShowLibrary();
                return;
            }
            if (string.Equals(message, "@exit", StringComparison.Ordinal))
            {
                ExitApplication();
                return;
            }
            OpenWindow(string.IsNullOrWhiteSpace(message) ? HomeUrl : message, true);
        }

        private void RefreshLibrary()
        {
            if (_exiting)
                return;
            PurgeExpiredRecycleBin();
            if (_library != null && !_library.IsDisposed)
            {
                _libraryRefreshTimer.Stop();
                _libraryRefreshTimer.Start();
            }
        }

        private void PurgeExpiredRecycleBin()
        {
            int before = State.RecycleBin.Count;
            State.RecycleBin.RemoveAll(delegate(DeletedWindowRecord item)
            {
                return item == null || item.Record == null || item.DeletedUtc < DateTime.UtcNow.AddDays(-30);
            });
            if (State.RecycleBin.Count != before)
                QueueSave();
        }

        private NoteImageRecord CreateNoteImageRecord(WindowRecord record, string extension, string originalFileName)
        {
            record.RepairPresentationDefaults();
            NoteImageRecord image = new NoteImageRecord();
            image.Id = Guid.NewGuid().ToString("N");
            image.FileName = image.Id + extension;
            image.OriginalFileName = Path.GetFileName(originalFileName);
            image.AddedUtc = DateTime.UtcNow;
            Directory.CreateDirectory(GetNoteFolder(record));
            return image;
        }

        private string GetNoteFolder(WindowRecord record)
        {
            string token = new string((record.Id ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
            if (token.Length == 0)
                token = "sin-id";
            string folder = Path.Combine(_paths.NotesDirectory, token);
            string root = Path.GetFullPath(_paths.NotesDirectory);
            string fullFolder = Path.GetFullPath(folder);
            if (!fullFolder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La carpeta de notas no es válida.");
            return fullFolder;
        }

        private string GetNoteDocumentPath(WindowRecord record)
        {
            string token = new string((record.Id ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
            if (token.Length == 0)
                token = "sin-id";
            string path = Path.Combine(_paths.NotesDirectory, token + ".rtf");
            string root = Path.GetFullPath(_paths.NotesDirectory);
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("El documento de nota no es válido.");
            return fullPath;
        }

        private static string GetSafeWindowToken(WindowRecord record)
        {
            string token = new string(((record == null ? string.Empty : record.Id) ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .ToArray());
            return token.Length == 0 ? "window" : token;
        }

        private static string NormalizeBookmarkFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return string.Empty;
            string[] pieces = folderPath
                .Replace('/', '\\')
                .Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(delegate(string part) { return part.Trim(); })
                .Where(delegate(string part) { return part.Length > 0; })
                .Take(12)
                .ToArray();
            return string.Join("\\", pieces);
        }

        private static string GetSafeWindowFilePath(string directory, WindowRecord record, string extension)
        {
            Directory.CreateDirectory(directory);
            string root = Path.GetFullPath(directory);
            string path = Path.Combine(root, GetSafeWindowToken(record) + extension);
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The local file path is not valid.");
            return fullPath;
        }

        private static IEnumerable<BookmarkImportItem> ParseBookmarks(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                yield break;

            List<BookmarkImportItem> chromeBookmarks = ParseChromeBookmarksJson(content);
            if (chromeBookmarks.Count > 0)
            {
                foreach (BookmarkImportItem bookmark in chromeBookmarks)
                    yield return bookmark;
                yield break;
            }

            Regex html = new Regex("<A\\s+[^>]*HREF\\s*=\\s*[\\\"'](?<url>[^\\\"']+)[\\\"'][^>]*>(?<title>.*?)</A>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match match in html.Matches(content))
            {
                BookmarkImportItem item = new BookmarkImportItem();
                item.Url = WebUtility.HtmlDecode(match.Groups["url"].Value).Trim();
                item.Title = WebUtility.HtmlDecode(Regex.Replace(match.Groups["title"].Value, "<.*?>", string.Empty)).Trim();
                item.FolderPath = string.Empty;
                if (!string.IsNullOrWhiteSpace(item.Url))
                    yield return item;
            }

            if (html.IsMatch(content))
                yield break;

            Regex jsonUrl = new Regex("\\\"url\\\"\\s*:\\s*\\\"(?<url>(?:\\\\.|[^\\\"])*)\\\"", RegexOptions.IgnoreCase);
            foreach (Match urlMatch in jsonUrl.Matches(content))
            {
                int start = Math.Max(0, urlMatch.Index - 1200);
                string prefix = content.Substring(start, urlMatch.Index - start);
                MatchCollection names = Regex.Matches(prefix, "\\\"name\\\"\\s*:\\s*\\\"(?<title>(?:\\\\.|[^\\\"])*)\\\"", RegexOptions.IgnoreCase);
                BookmarkImportItem item = new BookmarkImportItem();
                item.Url = DecodeJsonString(urlMatch.Groups["url"].Value);
                item.Title = names.Count == 0 ? item.Url : DecodeJsonString(names[names.Count - 1].Groups["title"].Value);
                item.FolderPath = string.Empty;
                if (!string.IsNullOrWhiteSpace(item.Url))
                    yield return item;
            }
        }

        private static List<BookmarkImportItem> ParseChromeBookmarksJson(string content)
        {
            List<BookmarkImportItem> results = new List<BookmarkImportItem>();
            if (string.IsNullOrWhiteSpace(content) || !content.TrimStart().StartsWith("{", StringComparison.Ordinal))
                return results;
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                Dictionary<string, object> root = serializer.DeserializeObject(content) as Dictionary<string, object>;
                if (root == null)
                    return results;
                object rootsObject;
                if (!root.TryGetValue("roots", out rootsObject))
                    return results;
                IDictionary roots = rootsObject as IDictionary;
                if (roots == null)
                    return results;
                foreach (DictionaryEntry entry in roots)
                    WalkChromeBookmarkNode(entry.Value as Dictionary<string, object>, string.Empty, results);
            }
            catch
            {
                results.Clear();
            }
            return results;
        }

        private static void WalkChromeBookmarkNode(
            Dictionary<string, object> node,
            string folderPath,
            List<BookmarkImportItem> results)
        {
            if (node == null)
                return;
            string name = GetBookmarkNodeValue(node, "name");
            string url = GetBookmarkNodeValue(node, "url");
            if (!string.IsNullOrWhiteSpace(url))
            {
                BookmarkImportItem item = new BookmarkImportItem();
                item.Url = url;
                item.Title = string.IsNullOrWhiteSpace(name) ? url : name;
                item.FolderPath = folderPath;
                results.Add(item);
                return;
            }

            string nextFolder = folderPath;
            if (!string.IsNullOrWhiteSpace(name))
                nextFolder = string.IsNullOrWhiteSpace(folderPath) ? name.Trim() : folderPath + "\\" + name.Trim();
            object childrenObject;
            if (!node.TryGetValue("children", out childrenObject))
                return;
            IEnumerable children = childrenObject as IEnumerable;
            if (children == null)
                return;
            foreach (object child in children)
                WalkChromeBookmarkNode(child as Dictionary<string, object>, nextFolder, results);
        }

        private static string GetBookmarkNodeValue(Dictionary<string, object> node, string key)
        {
            object value;
            if (!node.TryGetValue(key, out value) || value == null)
                return string.Empty;
            return Convert.ToString(value).Trim();
        }

        private static string DecodeJsonString(string value)
        {
            if (value == null)
                return string.Empty;
            string decoded = Regex.Unescape(value.Replace("\\/", "/"));
            return decoded.Trim();
        }

        private static HashSet<int> GetChildProcessIds(int rootProcessId)
        {
            Dictionary<int, List<int>> childrenByParent = new Dictionary<int, List<int>>();
            IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == INVALID_HANDLE_VALUE)
                return new HashSet<int>(new int[] { rootProcessId });

            try
            {
                PROCESSENTRY32 entry = new PROCESSENTRY32();
                entry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                if (Process32First(snapshot, ref entry))
                {
                    do
                    {
                        int parentId = unchecked((int)entry.th32ParentProcessID);
                        int processId = unchecked((int)entry.th32ProcessID);
                        List<int> children;
                        if (!childrenByParent.TryGetValue(parentId, out children))
                        {
                            children = new List<int>();
                            childrenByParent[parentId] = children;
                        }
                        children.Add(processId);
                        entry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                    }
                    while (Process32Next(snapshot, ref entry));
                }
            }
            finally
            {
                CloseHandle(snapshot);
            }

            HashSet<int> result = new HashSet<int>();
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(rootProcessId);
            while (queue.Count > 0)
            {
                int parentId = queue.Dequeue();
                if (!result.Add(parentId))
                    continue;
                List<int> children;
                if (childrenByParent.TryGetValue(parentId, out children))
                {
                    foreach (int child in children)
                        queue.Enqueue(child);
                }
            }
            return result;
        }

        private const uint TH32CS_SNAPPROCESS = 0x00000002;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private static string NormalizeUrl(string url)
        {
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "about"))
                return uri.AbsoluteUri;
            return HomeUrl;
        }

        private static bool IsInternalCommand(string value)
        {
            return value != null && value.StartsWith("@", StringComparison.Ordinal);
        }

        private static bool LooksLikeUsefulPage(Uri uri)
        {
            if (uri == null || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return false;
            string path = uri.AbsolutePath.Trim('/');
            return path.IndexOf("auth", StringComparison.OrdinalIgnoreCase) < 0 &&
                   path.IndexOf("login", StringComparison.OrdinalIgnoreCase) < 0 &&
                   path.IndexOf("settings", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static string CleanTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "Page";
            string cleaned = title.Replace(" | ChatGPT", string.Empty).Replace(" - ChatGPT", string.Empty).Trim();
            return cleaned.Length == 0 ? "Page" : cleaned;
        }
    }

    internal sealed class AltTriplePressHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int VK_MENU = 0x12;
        private readonly Action _onTriplePress;
        private readonly LowLevelKeyboardProc _callback;
        private IntPtr _hook;
        private bool _altDown;
        private int _pressCount;
        private DateTime _lastPressUtc;

        public AltTriplePressHook(Action onTriplePress)
        {
            _onTriplePress = onTriplePress;
            _callback = HandleKeyboard;
        }

        public void Start()
        {
            if (_hook != IntPtr.Zero)
                return;
            try
            {
                using (Process current = Process.GetCurrentProcess())
                {
                    IntPtr module = GetModuleHandle(current.MainModule == null ? null : current.MainModule.ModuleName);
                    _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _callback, module, 0);
                }
            }
            catch
            {
                _hook = IntPtr.Zero;
            }
        }

        private IntPtr HandleKeyboard(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && wParam != IntPtr.Zero)
            {
                int message = wParam.ToInt32();
                KbdLlHookStruct data = (KbdLlHookStruct)Marshal.PtrToStructure(lParam, typeof(KbdLlHookStruct));
                if (data.VkCode == VK_MENU)
                {
                    if ((message == WM_KEYDOWN || message == WM_SYSKEYDOWN) && !_altDown)
                    {
                        DateTime now = DateTime.UtcNow;
                        if ((now - _lastPressUtc).TotalMilliseconds > 900)
                            _pressCount = 0;
                        _lastPressUtc = now;
                        _pressCount++;
                        _altDown = true;
                        if (_pressCount >= 3)
                        {
                            _pressCount = 0;
                            if (_onTriplePress != null)
                                _onTriplePress();
                        }
                    }
                    else if (message == WM_KEYUP || message == WM_SYSKEYUP)
                        _altDown = false;
                }
            }
            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KbdLlHookStruct
        {
            public uint VkCode;
            public uint ScanCode;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);
    }

    internal sealed class MemorySnapshot
    {
        public long ApplicationWorkingSetBytes { get; set; }
        public int ApplicationProcessCount { get; set; }
        public ulong TotalPhysicalBytes { get; set; }
        public ulong AvailablePhysicalBytes { get; set; }
        public uint MemoryLoadPercent { get; set; }
    }

    internal sealed class BookmarkImportItem
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string FolderPath { get; set; }
    }
}
