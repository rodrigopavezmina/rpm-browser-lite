using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ChatGPTLite
{
    internal sealed class LibraryForm : Form
    {
        private readonly AppController _app;
        private readonly ListView _windowsList;
        private readonly ListView _recentsList;
        private readonly ListView _bookmarksList;
        private readonly ListView _recycleList;
        private readonly PictureBox _windowPreview;
        private readonly Label _previewCaption;
        private readonly Label _summary;
        private readonly TextBox _searchBox;
        private readonly TabControl _tabs;
        private ContextMenuStrip _moreMenu;
        private readonly ComboBox _categoryFilter;
        private bool _loadingCategoryFilter;
        private int _windowSortColumn = 6;
        private bool _windowSortDescending = true;

        public LibraryForm(AppController app)
        {
            _app = app;
            Text = "Windows, history and bookmarks · " + AppController.ProductName;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(820, 440);
            Size = new Size(1020, 590);
            Font = new Font("Segoe UI", 9f);
            KeyPreview = true;

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 122;
            header.Padding = new Padding(8, 7, 8, 5);

            FlowLayoutPanel firstRow = new FlowLayoutPanel();
            firstRow.Dock = DockStyle.Top;
            firstRow.Height = 37;
            firstRow.WrapContents = false;
            firstRow.AutoScroll = false;
            FlowLayoutPanel secondRow = new FlowLayoutPanel();
            secondRow.Dock = DockStyle.Top;
            secondRow.Height = 37;
            secondRow.WrapContents = false;
            secondRow.AutoScroll = false;

            Button newButton = CreateButton("✚ New window");
            Button openButton = CreateButton("↗ Open selected");
            Button hibernateButton = CreateButton("☾ Hibernate selected");
            Button caffeineButton = CreateButton("☕ Toggle caffeine");
            Button recycleButton = CreateButton("♲ Move selected to recycle");
            Button inactiveButton = CreateButton("☾ Hibernate inactive");
            Button customizeButton = CreateButton("✦ Title, ID and icon…");
            Button notesButton = CreateButton("☖ Show notes");
            Button lockButton = CreateButton("⊘ Permanent hibernate");
            Button optionsButton = CreateButton("⚙ Options");
            Button recycleAllButton = CreateButton("♲ Move all to recycle");
            newButton.Click += delegate { _app.OpenWindow(AppController.HomeUrl, true); RefreshContent(); };
            openButton.Click += delegate { OpenSelected(); };
            hibernateButton.Click += delegate { HibernateSelected(); };
            caffeineButton.Click += delegate { ToggleCaffeineSelected(); };
            recycleButton.Click += delegate { MoveSelectedToRecycle(); };
            inactiveButton.Click += async delegate { await _app.HibernateInactiveWindowsAsync(); RefreshContent(); };
            customizeButton.Click += delegate { CustomizeSelected(); };
            notesButton.Click += delegate { ShowNotesSelected(); };
            lockButton.Click += delegate { TogglePermanentHibernateSelected(); };
            optionsButton.Click += delegate { _app.ShowSettings(); };
            recycleAllButton.Click += delegate { _app.MoveSavedWindowsToRecycle(_app.SavedWindows); RefreshContent(); };
            firstRow.Controls.Add(newButton);
            firstRow.Controls.Add(openButton);
            firstRow.Controls.Add(hibernateButton);
            firstRow.Controls.Add(caffeineButton);
            firstRow.Controls.Add(recycleButton);
            secondRow.Controls.Add(inactiveButton);
            secondRow.Controls.Add(customizeButton);
            secondRow.Controls.Add(notesButton);
            secondRow.Controls.Add(lockButton);
            secondRow.Controls.Add(optionsButton);
            secondRow.Controls.Add(recycleAllButton);

            FlowLayoutPanel filterRow = new FlowLayoutPanel();
            filterRow.Dock = DockStyle.Top;
            filterRow.Height = 31;
            filterRow.WrapContents = false;
            filterRow.Padding = new Padding(0, 2, 0, 0);
            Label filterLabel = new Label();
            filterLabel.Text = "Category:";
            filterLabel.AutoSize = true;
            filterLabel.Margin = new Padding(2, 6, 5, 0);
            Label searchLabel = new Label();
            searchLabel.Text = "Search all:";
            searchLabel.AutoSize = true;
            searchLabel.Margin = new Padding(2, 6, 5, 0);
            _searchBox = new TextBox();
            _searchBox.Width = 330;
            _searchBox.Height = 24;
            _searchBox.Margin = new Padding(0, 2, 12, 0);
            _searchBox.Font = new Font("Segoe UI", 9f);
            _searchBox.AccessibleName = "Search open windows, recent pages, bookmarks and recycle bin";
            _searchBox.TextChanged += delegate { RefreshContent(); };
            _searchBox.KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.KeyCode == Keys.Enter)
                {
                    OpenSelected();
                    args.SuppressKeyPress = true;
                    args.Handled = true;
                }
                else if (args.KeyCode == Keys.Escape)
                {
                    _searchBox.Clear();
                    args.SuppressKeyPress = true;
                    args.Handled = true;
                }
            };
            _categoryFilter = new ComboBox();
            _categoryFilter.Width = 210;
            _categoryFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _categoryFilter.SelectedIndexChanged += delegate
            {
                if (!_loadingCategoryFilter)
                    RefreshContent();
            };
            filterRow.Controls.Add(searchLabel);
            filterRow.Controls.Add(_searchBox);
            filterRow.Controls.Add(filterLabel);
            filterRow.Controls.Add(_categoryFilter);
            header.Controls.Add(secondRow);
            header.Controls.Add(firstRow);
            header.Controls.Add(filterRow);

            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            TabControl tabs = _tabs;

            TabPage windowsPage = new TabPage("Open windows");
            _windowsList = CreateList();
            _windowsList.Columns.Add("Icon", 54);
            _windowsList.Columns.Add("ID", 92);
            _windowsList.Columns.Add("Title", 300);
            _windowsList.Columns.Add("Notes", 58);
            _windowsList.Columns.Add("State", 120);
            _windowsList.Columns.Add("Created", 145);
            _windowsList.Columns.Add("Last access", 145);
            _windowsList.DoubleClick += delegate { OpenSelectedWindow(); };
            _windowsList.ColumnClick += WindowsColumnClicked;
            _windowsList.KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.KeyCode == Keys.Enter)
                {
                    OpenSelectedWindow();
                    args.Handled = true;
                }
            };
            _windowsList.SelectedIndexChanged += delegate { UpdatePreviewPane(); };

            SplitContainer windowsSplit = new SplitContainer();
            windowsSplit.Dock = DockStyle.Fill;
            windowsSplit.Orientation = Orientation.Vertical;
            windowsSplit.SizeChanged += delegate
            {
                int maximum = windowsSplit.Width - 280;
                if (maximum >= 360)
                {
                    int desired = Math.Min(680, maximum);
                    if (windowsSplit.SplitterDistance != desired)
                        windowsSplit.SplitterDistance = desired;
                }
            };
            windowsSplit.Panel1.Controls.Add(_windowsList);

            Panel previewPanel = new Panel();
            previewPanel.Dock = DockStyle.Fill;
            previewPanel.Padding = new Padding(8);
            _previewCaption = new Label();
            _previewCaption.Dock = DockStyle.Top;
            _previewCaption.Height = 32;
            _previewCaption.Text = "Preview";
            _previewCaption.TextAlign = ContentAlignment.MiddleLeft;
            _previewCaption.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            _windowPreview = new PictureBox();
            _windowPreview.Dock = DockStyle.Fill;
            _windowPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _windowPreview.BorderStyle = BorderStyle.FixedSingle;
            _windowPreview.BackColor = Color.White;
            previewPanel.Controls.Add(_windowPreview);
            previewPanel.Controls.Add(_previewCaption);
            windowsSplit.Panel2.Controls.Add(previewPanel);
            windowsPage.Controls.Add(windowsSplit);

            TabPage recentsPage = new TabPage("Recent pages");
            _recentsList = CreateList();
            _recentsList.Columns.Add("Title", 410);
            _recentsList.Columns.Add("Last access", 155);
            _recentsList.Columns.Add("Address", 250);
            _recentsList.DoubleClick += delegate { OpenSelectedRecent(); };
            _recentsList.KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.KeyCode == Keys.Enter)
                {
                    OpenSelectedRecent();
                    args.SuppressKeyPress = true;
                    args.Handled = true;
                }
            };
            recentsPage.Controls.Add(_recentsList);

            FlowLayoutPanel recentActions = new FlowLayoutPanel();
            recentActions.Dock = DockStyle.Bottom;
            recentActions.Height = 46;
            recentActions.Padding = new Padding(7, 7, 7, 5);
            Button openRecentButton = CreateButton("✚ Open in new window");
            Button clearButton = CreateButton("× Clear local list");
            openRecentButton.Click += delegate { OpenSelectedRecent(); };
            clearButton.Click += delegate
            {
                if (MessageBox.Show(
                    "This only clears the local recent-page list. It does not affect any online account.\r\n\r\nContinue?",
                    "Borrar lista local",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes)
                    _app.ClearRecents();
            };
            recentActions.Controls.Add(openRecentButton);
            recentActions.Controls.Add(clearButton);
            recentsPage.Controls.Add(recentActions);

            TabPage bookmarksPage = new TabPage("Bookmarks");
            _bookmarksList = CreateList();
            _bookmarksList.Columns.Add("Title", 340);
            _bookmarksList.Columns.Add("Address", 370);
            _bookmarksList.Columns.Add("Added", 150);
            _bookmarksList.AllowDrop = true;
            _bookmarksList.DragEnter += BookmarkDragEnter;
            _bookmarksList.DragOver += BookmarkDragEnter;
            _bookmarksList.DragDrop += BookmarkDragDrop;
            _bookmarksList.DoubleClick += delegate { OpenSelectedBookmark(); };
            _bookmarksList.KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.KeyCode == Keys.Enter)
                {
                    OpenSelectedBookmark();
                    args.SuppressKeyPress = true;
                    args.Handled = true;
                }
            };
            bookmarksPage.Controls.Add(_bookmarksList);

            FlowLayoutPanel bookmarkActions = new FlowLayoutPanel();
            bookmarkActions.Dock = DockStyle.Bottom;
            bookmarkActions.Height = 46;
            bookmarkActions.Padding = new Padding(7, 7, 7, 5);
            Button addBookmarkButton = CreateButton("★ Bookmark selected window");
            Button openBookmarkButton = CreateButton("↗ Open bookmark");
            Button importBookmarksButton = CreateButton("↓ Import Chrome file");
            Button exportBookmarksButton = CreateButton("↑ Export HTML");
            Button removeBookmarkButton = CreateButton("× Remove");
            addBookmarkButton.Click += delegate { AddSelectedWindowBookmark(); };
            openBookmarkButton.Click += delegate { OpenSelectedBookmark(); };
            importBookmarksButton.Click += delegate { ImportBookmarks(); };
            exportBookmarksButton.Click += delegate { ExportBookmarks(); };
            removeBookmarkButton.Click += delegate { RemoveSelectedBookmarks(); };
            bookmarkActions.Controls.Add(addBookmarkButton);
            bookmarkActions.Controls.Add(openBookmarkButton);
            bookmarkActions.Controls.Add(importBookmarksButton);
            bookmarkActions.Controls.Add(exportBookmarksButton);
            bookmarkActions.Controls.Add(removeBookmarkButton);
            bookmarksPage.Controls.Add(bookmarkActions);

            TabPage recyclePage = new TabPage("Recycle bin (30 days)");
            _recycleList = CreateList();
            _recycleList.Columns.Add("Title", 360);
            _recycleList.Columns.Add("ID", 100);
            _recycleList.Columns.Add("Removed", 160);
            _recycleList.Columns.Add("Address", 300);
            _recycleList.DoubleClick += delegate { OpenSelectedRecycle(); };
            _recycleList.KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.KeyCode == Keys.Enter)
                {
                    OpenSelectedRecycle();
                    args.SuppressKeyPress = true;
                    args.Handled = true;
                }
            };
            recyclePage.Controls.Add(_recycleList);

            FlowLayoutPanel recycleActions = new FlowLayoutPanel();
            recycleActions.Dock = DockStyle.Bottom;
            recycleActions.Height = 46;
            recycleActions.Padding = new Padding(7, 7, 7, 5);
            Button restoreRecycleButton = CreateButton("↩ Restore selected");
            Button emptyRecycleButton = CreateButton("× Empty recycle bin");
            restoreRecycleButton.Click += delegate { RestoreSelectedRecycle(); };
            emptyRecycleButton.Click += delegate { EmptyRecycleBin(); };
            recycleActions.Controls.Add(restoreRecycleButton);
            recycleActions.Controls.Add(emptyRecycleButton);
            recyclePage.Controls.Add(recycleActions);

            tabs.TabPages.Add(windowsPage);
            tabs.TabPages.Add(recentsPage);
            tabs.TabPages.Add(bookmarksPage);
            tabs.TabPages.Add(recyclePage);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 36;
            footer.Padding = new Padding(12, 4, 12, 4);
            _summary = new Label();
            _summary.Dock = DockStyle.Fill;
            _summary.TextAlign = ContentAlignment.MiddleLeft;
            _summary.ForeColor = Color.FromArgb(90, 90, 90);
            footer.Controls.Add(_summary);

            Controls.Add(tabs);
            Controls.Add(footer);
            Controls.Add(header);
            Shown += delegate { RefreshContent(); };
            Activated += delegate { RefreshContent(); };
            KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.Control && args.KeyCode == Keys.F)
                {
                    _searchBox.Focus();
                    _searchBox.SelectAll();
                    args.SuppressKeyPress = true;
                    args.Handled = true;
                }
            };
            FormClosed += delegate
            {
                if (_moreMenu != null && !_moreMenu.IsDisposed)
                    _moreMenu.Dispose();
                _moreMenu = null;
                if (_windowPreview != null && _windowPreview.Image != null)
                {
                    _windowPreview.Image.Dispose();
                    _windowPreview.Image = null;
                }
            };
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            if (IsDisposed || Disposing)
                return;
            UiTheme.ApplyNativeTree(this, _app.State.Settings.DarkMode);
            UiTheme.ApplyWindowChrome(this, _app.State.Settings.DarkMode);
            _summary.ForeColor = _app.State.Settings.DarkMode ? UiTheme.DarkMutedText : Color.FromArgb(90, 90, 90);
            _previewCaption.BackColor = _app.State.Settings.DarkMode ? UiTheme.DarkSurface : UiTheme.LightBackground;
            _previewCaption.ForeColor = _app.State.Settings.DarkMode ? UiTheme.DarkText : UiTheme.LightText;
            _windowPreview.BackColor = _app.State.Settings.DarkMode ? UiTheme.DarkInput : Color.White;
        }

        public void FocusSearch()
        {
            if (IsDisposed || Disposing || _searchBox == null || _searchBox.IsDisposed)
                return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!IsDisposed && !Disposing && _searchBox != null && !_searchBox.IsDisposed)
                    {
                        _searchBox.Focus();
                        _searchBox.SelectAll();
                    }
                });
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
                // The manager can be closing while a tray click is being delivered.
            }
        }

        public void RefreshContent()
        {
            if (IsDisposed || Disposing || _windowsList == null || _windowsList.IsDisposed)
                return;

            string selectedCategory = _categoryFilter.SelectedItem as string;
            _loadingCategoryFilter = true;
            _categoryFilter.Items.Clear();
            _categoryFilter.Items.Add("All categories");
            foreach (string category in _app.SavedWindows
                .Select(delegate(WindowRecord record) { return (record.Category ?? string.Empty).Trim(); })
                .Where(delegate(string category) { return category.Length > 0; })
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(delegate(string category) { return category; }))
                _categoryFilter.Items.Add(category);
            int categoryIndex = 0;
            if (!string.IsNullOrWhiteSpace(selectedCategory))
            {
                for (int index = 1; index < _categoryFilter.Items.Count; index++)
                {
                    if (string.Equals(_categoryFilter.Items[index] as string, selectedCategory, StringComparison.CurrentCultureIgnoreCase))
                    {
                        categoryIndex = index;
                        break;
                    }
                }
            }
            _categoryFilter.SelectedIndex = categoryIndex;
            _loadingCategoryFilter = false;

            string search = _searchBox == null ? string.Empty : (_searchBox.Text ?? string.Empty).Trim();

            IList<WindowRecord> visibleWindows = _app.SavedWindows;
            if (categoryIndex > 0)
            {
                string filter = _categoryFilter.Items[categoryIndex] as string;
                visibleWindows = visibleWindows.Where(delegate(WindowRecord record)
                {
                    return string.Equals((record.Category ?? string.Empty).Trim(), filter, StringComparison.CurrentCultureIgnoreCase);
                }).ToList();
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                visibleWindows = visibleWindows.Where(delegate(WindowRecord record)
                {
                    BrowserWindow live = _app.GetOpenWindow(record.Id);
                    string liveTitle = live == null ? string.Empty : live.DisplayTitle;
                    return MatchesLibrarySearch(search,
                        record.Title,
                        record.CustomTitle,
                        liveTitle,
                        record.Identifier,
                        record.Url,
                        record.Category,
                        record.NoteText);
                }).ToList();
            }

            _windowsList.BeginUpdate();
            _windowsList.Items.Clear();
            foreach (WindowRecord record in visibleWindows)
            {
                BrowserWindow window = _app.GetOpenWindow(record.Id);
                string state;
                Color stateColor;
                if (record.Hibernated || record.KeepHibernated)
                {
                    state = "● Hibernated";
                    stateColor = Color.FromArgb(210, 65, 72);
                }
                else if (record.CaffeineMode)
                {
                    state = window == null ? "● Caffeine · saved" : "● Caffeine · awake";
                    stateColor = Color.FromArgb(54, 164, 91);
                }
                else if (window != null && window.IsSuspended)
                {
                    state = "● Sleeping";
                    stateColor = Color.FromArgb(220, 180, 42);
                }
                else if (window == null)
                {
                    state = "Stored · not loaded";
                    stateColor = Color.FromArgb(90, 125, 165);
                }
                else
                {
                    state = "● Active";
                    stateColor = Color.FromArgb(54, 164, 91);
                }
                ListViewItem item = new ListViewItem(new string[]
                {
                    IconVisuals.GetGlyph(record),
                    string.IsNullOrWhiteSpace(record.Identifier) ? "—" : record.Identifier,
                    window == null ? record.DisplayTitle(record.Title) : window.DisplayTitle,
                    (window != null ? window.HasNotes : (!string.IsNullOrWhiteSpace(record.NoteText) || (record.NoteImages != null && record.NoteImages.Count > 0))) ? "Yes" : "—",
                    state,
                    LocalTime(record.CreatedUtc),
                    LocalTime(record.LastActiveUtc)
                });
                item.Tag = record.Id;
                item.BackColor = _app.State.Settings.DarkMode
                    ? IconVisuals.DarkTint(stateColor)
                    : IconVisuals.PaleColor(stateColor);
                item.ForeColor = _app.State.Settings.DarkMode ? UiTheme.DarkText : Color.FromArgb(40, 40, 40);
                _windowsList.Items.Add(item);
            }
            _windowsList.ListViewItemSorter = new WindowListComparer(_windowSortColumn, _windowSortDescending);
            _windowsList.Sort();
            _windowsList.EndUpdate();
            UpdatePreviewPane();

            _recentsList.BeginUpdate();
            _recentsList.Items.Clear();
            foreach (RecentRecord recent in _app.State.Recents
                .Where(delegate(RecentRecord item)
                {
                    return string.IsNullOrWhiteSpace(search) || MatchesLibrarySearch(search, item.Title, item.Url);
                })
                .OrderByDescending(delegate(RecentRecord item) { return item.LastOpenedUtc; }))
            {
                ListViewItem item = new ListViewItem(new string[]
                {
                    string.IsNullOrWhiteSpace(recent.Title) ? "Page" : recent.Title,
                    LocalTime(recent.LastOpenedUtc),
                    recent.Url
                });
                item.Tag = recent.Url;
                _recentsList.Items.Add(item);
            }
            _recentsList.EndUpdate();

            _bookmarksList.BeginUpdate();
            _bookmarksList.Items.Clear();
            foreach (BookmarkRecord bookmark in _app.State.Bookmarks
                .Where(delegate(BookmarkRecord item)
                {
                    return string.IsNullOrWhiteSpace(search) || MatchesLibrarySearch(search, item.Title, item.Url, item.FolderPath);
                })
                .OrderBy(delegate(BookmarkRecord item) { return item.Title; }))
            {
                ListViewItem item = new ListViewItem(new string[]
                {
                    string.IsNullOrWhiteSpace(bookmark.Title) ? bookmark.Url : bookmark.Title,
                    bookmark.Url,
                    LocalTime(bookmark.CreatedUtc)
                });
                item.Tag = bookmark;
                _bookmarksList.Items.Add(item);
            }
            _bookmarksList.EndUpdate();

            _recycleList.BeginUpdate();
            _recycleList.Items.Clear();
            foreach (DeletedWindowRecord deleted in _app.State.RecycleBin
                .Where(delegate(DeletedWindowRecord item) { return item != null && item.Record != null; })
                .Where(delegate(DeletedWindowRecord item)
                {
                    WindowRecord record = item.Record;
                    return string.IsNullOrWhiteSpace(search) || MatchesLibrarySearch(search,
                        record.Title,
                        record.CustomTitle,
                        record.Identifier,
                        record.Url,
                        record.Category,
                        record.NoteText);
                })
                .OrderByDescending(delegate(DeletedWindowRecord item) { return item.DeletedUtc; }))
            {
                WindowRecord record = deleted.Record;
                ListViewItem item = new ListViewItem(new string[]
                {
                    record.DisplayTitle(record.Title),
                    string.IsNullOrWhiteSpace(record.Identifier) ? "—" : record.Identifier,
                    LocalTime(deleted.DeletedUtc),
                    record.Url
                });
                item.Tag = deleted;
                _recycleList.Items.Add(item);
            }
            _recycleList.EndUpdate();

            int hibernated = _app.SavedWindows.Count(delegate(WindowRecord record) { return record.Hibernated || record.KeepHibernated; });
            int caffeine = _app.SavedWindows.Count(delegate(WindowRecord record) { return record.CaffeineMode; });
            int loaded = _app.OpenWindows.Count;
            _summary.Text = _app.SavedWindows.Count + " saved window(s) · " + loaded + " loaded · " + caffeine + " caffeine · " +
                            hibernated + " hibernated · " + _app.State.Bookmarks.Count + " bookmark(s) · " +
                            _app.State.RecycleBin.Count + " in recycle bin" +
                            (string.IsNullOrWhiteSpace(search) ? string.Empty : " Â· filter: " + search);
        }

        private void UpdatePreviewPane()
        {
            if (_windowPreview == null || _windowPreview.IsDisposed)
                return;

            Image previous = _windowPreview.Image;
            _windowPreview.Image = null;
            if (previous != null)
                previous.Dispose();

            if (_windowsList.SelectedItems.Count == 0)
            {
                _previewCaption.Text = "Preview · select a window";
                return;
            }

            string id = _windowsList.SelectedItems[0].Tag as string;
            WindowRecord record = _app.SavedWindows.FirstOrDefault(
                delegate(WindowRecord item) { return item != null && string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase); });
            if (record == null)
            {
                _previewCaption.Text = "Preview";
                return;
            }

            string path = _app.GetLatestWindowPreviewPath(record);
            _previewCaption.Text = "Preview · " + record.DisplayTitle(record.Title);
            if (!File.Exists(path))
                return;

            try
            {
                using (Image source = Image.FromFile(path))
                    _windowPreview.Image = CreatePreviewThumbnail(source);
            }
            catch (Exception ex)
            {
                _app.Log("No se pudo cargar la vista previa del administrador", ex);
            }
        }

        private static Image CreatePreviewThumbnail(Image source)
        {
            const int maximumWidth = 1200;
            const int maximumHeight = 900;
            float scale = Math.Min(1f, Math.Min(
                (float)maximumWidth / Math.Max(1, source.Width),
                (float)maximumHeight / Math.Max(1, source.Height)));
            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));
            return new Bitmap(source, new Size(width, height));
        }

        private void OpenSelected()
        {
            // The toolbar button receives focus when clicked, so rely on the active tab
            // and selected rows instead of the control that currently has focus.
            if (_tabs == null)
                return;
            if (_tabs.SelectedIndex == 0)
                OpenSelectedWindow();
            else if (_tabs.SelectedIndex == 1)
                OpenSelectedRecent();
            else if (_tabs.SelectedIndex == 2)
                OpenSelectedBookmark();
            else if (_tabs.SelectedIndex == 3)
                OpenSelectedRecycle();
        }

        private void OpenSelectedWindow()
        {
            string[] ids = _windowsList.SelectedItems
                .Cast<ListViewItem>()
                .Select(delegate(ListViewItem item) { return item.Tag as string; })
                .Where(delegate(string id) { return !string.IsNullOrWhiteSpace(id); })
                .ToArray();
            if (ids.Length == 0)
                return;
            if (ids.Length > 6 && MessageBox.Show(
                "Opening " + ids.Length + " browser windows can use a lot of memory on a slow computer. Continue?",
                AppController.ProductName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            BrowserWindow last = null;
            List<string> failed = new List<string>();
            foreach (string id in ids)
            {
                try
                {
                    last = _app.OpenSavedWindow(id, false) ?? last;
                }
                catch (Exception ex)
                {
                    _app.Log("No se pudo abrir una ventana seleccionada", ex);
                    failed.Add(id);
                }
            }
            if (last != null)
                last.ActivateAndRestore();
            RefreshContent();
            if (failed.Count > 0)
                MessageBox.Show(
                    failed.Count + " ventana(s) no pudieron abrirse y se mantuvieron guardadas en el administrador.",
                    AppController.ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
        }

        private void OpenSelectedRecent()
        {
            if (_recentsList.SelectedItems.Count == 0)
                return;
            OpenUrlSelection(_recentsList.SelectedItems
                .Cast<ListViewItem>()
                .Select(delegate(ListViewItem item) { return item.Tag as string; }));
        }

        private void HibernateSelected()
        {
            _app.MarkSavedWindowsAsHibernated(SelectedRecords());
            RefreshContent();
        }

        private void MoveSelectedToRecycle()
        {
            WindowRecord[] records = SelectedRecords();
            if (records.Length == 0)
                return;
            _app.MoveSavedWindowsToRecycle(records);
            RefreshContent();
        }

        private void RestoreSelectedRecycle()
        {
            DeletedWindowRecord[] deleted = _recycleList.SelectedItems
                .Cast<ListViewItem>()
                .Select(delegate(ListViewItem item) { return item.Tag as DeletedWindowRecord; })
                .Where(delegate(DeletedWindowRecord item) { return item != null; })
                .ToArray();
            if (deleted.Length == 0)
                return;
            _app.RestoreRecycledWindows(deleted);
            RefreshContent();
        }

        private void EmptyRecycleBin()
        {
            if (_app.State.RecycleBin.Count == 0)
                return;
            if (MessageBox.Show(
                "Remove every item from the recycle bin now? This cannot be undone.",
                AppController.ProductName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes)
                _app.EmptyRecycleBin();
            RefreshContent();
        }

        private void ShowMoreMenu(Control owner)
        {
            if (IsDisposed || Disposing || owner == null || owner.IsDisposed)
                return;
            if (_moreMenu != null && !_moreMenu.IsDisposed)
                _moreMenu.Close();
            _moreMenu = null;
            ContextMenuStrip menu = new ContextMenuStrip();
            _moreMenu = menu;
            menu.Closed += delegate
            {
                if (ReferenceEquals(_moreMenu, menu))
                    _moreMenu = null;
                if (!menu.IsDisposed)
                    menu.Dispose();
            };
            menu.Items.Add("☾ Hibernate inactive", null, async delegate { await _app.HibernateInactiveWindowsAsync(); RefreshContent(); });
            menu.Items.Add("✦ Title, ID and icon…", null, delegate { CustomizeSelected(); });
            menu.Items.Add("✎ Show notes", null, delegate { ShowNotesSelected(); });
            menu.Items.Add("⊘ Toggle permanently hibernated", null, delegate { TogglePermanentHibernateSelected(); });
            menu.Items.Add("⚙ Options", null, delegate { _app.ShowSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("♲ Move all windows to recycle", null, delegate
            {
                _app.MoveSavedWindowsToRecycle(_app.SavedWindows);
                RefreshContent();
            });
            try
            {
                if (!IsDisposed && !Disposing && !owner.IsDisposed)
                    menu.Show(owner, new Point(0, owner.Height));
            }
            catch (ObjectDisposedException)
            {
                if (!menu.IsDisposed)
                    menu.Dispose();
            }
        }

        private void CustomizeSelected()
        {
            BrowserWindow window = SelectedWindow();
            if (window == null)
                return;
            window.ShowCustomization();
            RefreshContent();
        }

        private void ShowNotesSelected()
        {
            BrowserWindow window = SelectedWindow();
            if (window == null)
                return;
            window.ActivateAndRestore();
            window.ShowNotes();
            RefreshContent();
        }

        private void ToggleCaffeineSelected()
        {
            _app.ToggleSavedCaffeine(SelectedRecords());
            RefreshContent();
        }

        private void TogglePermanentHibernateSelected()
        {
            _app.ToggleSavedPermanentHibernate(SelectedRecords());
            RefreshContent();
        }

        private void AddSelectedWindowBookmark()
        {
            WindowRecord record = SelectedRecord();
            if (record == null)
                return;
            BrowserWindow window = _app.GetOpenWindow(record.Id);
            _app.AddBookmark(record.Url, window == null ? record.DisplayTitle(record.Title) : window.DisplayTitle);
            RefreshContent();
        }

        private void OpenSelectedBookmark()
        {
            if (_bookmarksList.SelectedItems.Count == 0)
                return;
            OpenUrlSelection(_bookmarksList.SelectedItems
                .Cast<ListViewItem>()
                .Select(delegate(ListViewItem item)
                {
                    BookmarkRecord bookmark = item.Tag as BookmarkRecord;
                    return bookmark == null ? string.Empty : bookmark.Url;
                }));
        }

        private void OpenSelectedRecycle()
        {
            DeletedWindowRecord[] deleted = _recycleList.SelectedItems
                .Cast<ListViewItem>()
                .Select(delegate(ListViewItem item) { return item.Tag as DeletedWindowRecord; })
                .Where(delegate(DeletedWindowRecord item) { return item != null && item.Record != null; })
                .ToArray();
            if (deleted.Length == 0)
                return;
            if (deleted.Length > 6 && MessageBox.Show(
                "Opening " + deleted.Length + " recycled windows can use a lot of memory. Continue?",
                AppController.ProductName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            _app.RestoreRecycledWindows(deleted);
            BrowserWindow last = null;
            foreach (DeletedWindowRecord item in deleted)
            {
                if (item == null || item.Record == null)
                    continue;
                last = _app.OpenSavedWindow(item.Record.Id, false) ?? last;
            }
            if (last != null)
                last.ActivateAndRestore();
            RefreshContent();
        }

        private void OpenUrlSelection(IEnumerable<string> urls)
        {
            string[] selected = (urls ?? Enumerable.Empty<string>())
                .Where(delegate(string url) { return !string.IsNullOrWhiteSpace(url); })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (selected.Length == 0)
                return;
            if (selected.Length > 6 && MessageBox.Show(
                "Opening " + selected.Length + " pages can use a lot of memory on a slow computer. Continue?",
                AppController.ProductName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            BrowserWindow last = null;
            foreach (string url in selected)
            {
                try
                {
                    last = _app.OpenWindow(url, false) ?? last;
                }
                catch (Exception ex)
                {
                    _app.Log("No se pudo abrir una página seleccionada", ex);
                }
            }
            if (last != null)
                last.ActivateAndRestore();
            RefreshContent();
        }

        private void RemoveSelectedBookmarks()
        {
            BookmarkRecord[] bookmarks = _bookmarksList.SelectedItems
                .Cast<ListViewItem>()
                .Select(delegate(ListViewItem item) { return item.Tag as BookmarkRecord; })
                .Where(delegate(BookmarkRecord bookmark) { return bookmark != null; })
                .ToArray();
            if (bookmarks.Length == 0)
                return;
            _app.RemoveBookmarks(bookmarks);
            RefreshContent();
        }

        private void ImportBookmarks()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Import bookmarks";
                dialog.Filter = "Chrome bookmarks (*.html;*.htm)|*.html;*.htm|Bookmark files (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    int added = _app.ImportBookmarks(dialog.FileName);
                    MessageBox.Show(added + " new bookmark(s) imported.", AppController.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The bookmark file could not be imported.\r\n\r\n" + ex.Message, AppController.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            RefreshContent();
        }

        private void ExportBookmarks()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Export bookmarks";
                dialog.Filter = "HTML bookmarks (*.html)|*.html";
                dialog.FileName = "RPM Browser Lite Bookmarks.html";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    _app.ExportBookmarks(dialog.FileName);
                    MessageBox.Show("Bookmarks exported.", AppController.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The bookmarks could not be exported.\r\n\r\n" + ex.Message, AppController.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private static void BookmarkDragEnter(object sender, DragEventArgs args)
        {
            string url;
            string title;
            args.Effect = TryReadDroppedBookmark(args.Data, out url, out title)
                ? DragDropEffects.Link
                : DragDropEffects.None;
        }

        private void BookmarkDragDrop(object sender, DragEventArgs args)
        {
            string url;
            string title;
            if (!TryReadDroppedBookmark(args.Data, out url, out title))
                return;
            try
            {
                _app.AddBookmark(url, title);
                RefreshContent();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, AppController.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static bool TryReadDroppedBookmark(IDataObject data, out string url, out string title)
        {
            url = string.Empty;
            title = string.Empty;
            if (data == null)
                return false;

            string html = data.GetData(DataFormats.Html, false) as string;
            if (!string.IsNullOrWhiteSpace(html))
            {
                Match anchor = Regex.Match(html, "<a[^>]+href\\s*=\\s*[\\\"'](?<url>[^\\\"']+)[\\\"'][^>]*>(?<title>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (anchor.Success)
                {
                    url = WebUtility.HtmlDecode(anchor.Groups["url"].Value).Trim();
                    title = Regex.Replace(WebUtility.HtmlDecode(anchor.Groups["title"].Value), "<[^>]+>", string.Empty).Trim();
                }
            }

            if (url.Length == 0)
            {
                string text = data.GetData(DataFormats.UnicodeText, false) as string;
                if (string.IsNullOrWhiteSpace(text))
                    text = data.GetData(DataFormats.Text, false) as string;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    foreach (string line in text.Split(new char[] { '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        Uri candidate;
                        if (Uri.TryCreate(line.Trim(), UriKind.Absolute, out candidate) &&
                            (candidate.Scheme == Uri.UriSchemeHttp || candidate.Scheme == Uri.UriSchemeHttps))
                        {
                            url = candidate.AbsoluteUri;
                            break;
                        }
                    }
                }
            }

            Uri parsed;
            if (!Uri.TryCreate(url, UriKind.Absolute, out parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                return false;
            if (string.IsNullOrWhiteSpace(title))
                title = parsed.Host;
            return true;
        }

        private BrowserWindow SelectedWindow()
        {
            WindowRecord record = SelectedRecord();
            if (record == null)
                return null;
            return _app.OpenSavedWindow(record.Id, true);
        }

        private WindowRecord SelectedRecord()
        {
            if (_windowsList.SelectedItems.Count == 0)
            {
                MessageBox.Show("Select a window first.", "Windows and recent", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            string id = _windowsList.SelectedItems[0].Tag as string;
            return _app.SavedWindows.FirstOrDefault(delegate(WindowRecord candidate) { return candidate.Id == id; });
        }

        private WindowRecord[] SelectedRecords()
        {
            return _windowsList.SelectedItems
                .Cast<ListViewItem>()
                .Select(delegate(ListViewItem item) { return item.Tag as string; })
                .Where(delegate(string id) { return !string.IsNullOrWhiteSpace(id); })
                .Select(delegate(string id) { return _app.SavedWindows.FirstOrDefault(delegate(WindowRecord candidate) { return candidate.Id == id; }); })
                .Where(delegate(WindowRecord record) { return record != null; })
                .ToArray();
        }

        private void WindowsColumnClicked(object sender, ColumnClickEventArgs e)
        {
            if (_windowSortColumn == e.Column)
                _windowSortDescending = !_windowSortDescending;
            else
            {
                _windowSortColumn = e.Column;
                _windowSortDescending = e.Column == 5 || e.Column == 6;
            }
            RefreshContent();
        }

        private static ListView CreateList()
        {
            ListView list = new ListView();
            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.Font = new Font("Segoe UI", 9.5f);
            list.FullRowSelect = true;
            list.MultiSelect = true;
            list.HideSelection = false;
            list.BorderStyle = BorderStyle.None;
            list.AccessibleRole = AccessibleRole.Table;
            // A 1-pixel image with a 32-pixel height gives rows a comfortable
            // touch target without changing the data model or drawing custom cells.
            list.SmallImageList = new ImageList();
            list.SmallImageList.ImageSize = new Size(1, 32);
            return list;
        }

        private static bool MatchesLibrarySearch(string query, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            string[] terms = query
                .Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string term in terms)
            {
                bool found = false;
                foreach (string value in values ?? new string[0])
                {
                    if (!string.IsNullOrWhiteSpace(value) &&
                        value.IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
        }

        private static Button CreateButton(string text)
        {
            Button button = new Button();
            button.AutoSize = true;
            button.Height = 34;
            button.Padding = new Padding(10, 3, 10, 3);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(150, 160, 175);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 235, 248);
            button.Cursor = Cursors.Hand;
            button.AutoEllipsis = true;
            button.UseCompatibleTextRendering = false;
            button.AccessibleRole = AccessibleRole.PushButton;
            button.TabStop = true;
            button.Text = text;
            return button;
        }

        private static string LocalTime(DateTime utc)
        {
            if (utc == DateTime.MinValue)
                return "—";
            return utc.ToLocalTime().ToString("dd-MM-yyyy HH:mm");
        }

        private sealed class WindowListComparer : IComparer
        {
            private readonly int _column;
            private readonly bool _descending;

            public WindowListComparer(int column, bool descending)
            {
                _column = column;
                _descending = descending;
            }

            public int Compare(object left, object right)
            {
                ListViewItem first = left as ListViewItem;
                ListViewItem second = right as ListViewItem;
                if (first == null || second == null)
                    return 0;

                string a = first.SubItems.Count > _column ? first.SubItems[_column].Text : string.Empty;
                string b = second.SubItems.Count > _column ? second.SubItems[_column].Text : string.Empty;
                int comparison;
                if (_column == 5 || _column == 6)
                {
                    DateTime firstDate;
                    DateTime secondDate;
                    DateTime.TryParse(a, out firstDate);
                    DateTime.TryParse(b, out secondDate);
                    comparison = DateTime.Compare(firstDate, secondDate);
                }
                else
                {
                    comparison = string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);
                }
                return _descending ? -comparison : comparison;
            }
        }
    }
}
