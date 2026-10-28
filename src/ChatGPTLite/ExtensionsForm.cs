using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ChatGPTLite
{
    /// <summary>
    /// Small, deliberately local-only extension manager. WebView2 accepts unpacked
    /// Chromium/Edge extensions (a folder containing manifest.json), not Chrome Web
    /// Store URLs or opaque CRX files. Keeping this explicit avoids silently granting
    /// an arbitrary downloaded extension access to every page in the shared profile.
    /// </summary>
    internal sealed class ExtensionsForm : Form
    {
        private readonly AppController _app;
        private readonly CheckBox _enabled;
        private readonly ListBox _paths;
        private readonly Label _status;
        private readonly List<string> _workingPaths;

        public ExtensionsForm(AppController app)
        {
            _app = app;
            Text = "Edge / Chromium extensions · " + AppController.ProductName;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(680, 430);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9f);

            Label heading = new Label();
            heading.Text = "Edge / Chromium extensions";
            heading.Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold);
            heading.AutoSize = true;
            heading.Location = new Point(22, 18);

            Label explanation = new Label();
            explanation.Text = "Add trusted unpacked extensions exported from Microsoft Edge or Chromium.\r\n"
                + "Choose the folder that contains manifest.json. ZIP/CRX files must be extracted first.";
            explanation.AutoSize = false;
            explanation.Size = new Size(630, 42);
            explanation.Location = new Point(24, 51);
            explanation.ForeColor = Color.FromArgb(90, 90, 90);

            _enabled = new CheckBox();
            _enabled.Text = "Enable local extensions in WebView2";
            _enabled.AutoSize = true;
            _enabled.Location = new Point(25, 101);
            _enabled.Checked = _app.State.Settings.BrowserExtensionsEnabled == true;

            _paths = new ListBox();
            _paths.Location = new Point(25, 135);
            _paths.Size = new Size(630, 155);
            _paths.HorizontalScrollbar = true;
            _paths.IntegralHeight = false;
            _workingPaths = (_app.State.Settings.BrowserExtensionPaths ?? new List<string>())
                .Where(delegate(string path) { return !string.IsNullOrWhiteSpace(path); })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            RefreshPathList();

            Button add = new Button();
            add.Text = "Add extension folder…";
            add.AutoSize = true;
            add.Location = new Point(25, 305);
            add.Click += delegate { AddFolder(); };

            Button remove = new Button();
            remove.Text = "Remove selected";
            remove.AutoSize = true;
            remove.Location = new Point(190, 305);
            remove.Click += delegate { RemoveSelected(); };

            Button open = new Button();
            open.Text = "Open selected folder";
            open.AutoSize = true;
            open.Location = new Point(322, 305);
            open.Click += delegate { OpenSelected(); };

            _status = new Label();
            _status.Text = "Changes are applied the next time RPM Browser Lite starts.";
            _status.AutoSize = false;
            _status.Size = new Size(630, 40);
            _status.Location = new Point(25, 346);
            _status.ForeColor = Color.FromArgb(90, 90, 90);

            Button save = new Button();
            save.Text = "Save";
            save.Width = 100;
            save.Height = 30;
            save.Location = new Point(450, 388);
            save.Click += delegate { SaveChanges(); };

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.Width = 100;
            cancel.Height = 30;
            cancel.Location = new Point(555, 388);
            cancel.DialogResult = DialogResult.Cancel;

            Controls.Add(heading);
            Controls.Add(explanation);
            Controls.Add(_enabled);
            Controls.Add(_paths);
            Controls.Add(add);
            Controls.Add(remove);
            Controls.Add(open);
            Controls.Add(_status);
            Controls.Add(save);
            Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;

            Shown += delegate { ApplyTheme(); };
        }

        private void RefreshPathList()
        {
            _paths.Items.Clear();
            foreach (string path in _workingPaths)
                _paths.Items.Add(DescribePath(path));
        }

        private string DescribePath(string path)
        {
            string manifest = Path.Combine(path, "manifest.json");
            if (!File.Exists(manifest))
                return "⚠ " + path;
            return "✓ " + path;
        }

        private void AddFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose an unpacked Edge/Chromium extension folder containing manifest.json";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                string path;
                try { path = Path.GetFullPath(dialog.SelectedPath); }
                catch { return; }
                if (!File.Exists(Path.Combine(path, "manifest.json")))
                {
                    MessageBox.Show(this,
                        "This folder does not contain manifest.json. Extract the extension first and select its top folder.",
                        "Invalid extension folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!_workingPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                    _workingPaths.Add(path);
                RefreshPathList();
            }
        }

        private void RemoveSelected()
        {
            int index = _paths.SelectedIndex;
            if (index < 0 || index >= _workingPaths.Count)
                return;
            _workingPaths.RemoveAt(index);
            RefreshPathList();
        }

        private void OpenSelected()
        {
            int index = _paths.SelectedIndex;
            if (index < 0 || index >= _workingPaths.Count)
                return;
            try { Process.Start(_workingPaths[index]); }
            catch { }
        }

        private void SaveChanges()
        {
            _app.State.Settings.BrowserExtensionsEnabled = _enabled.Checked;
            _app.State.Settings.BrowserExtensionPaths = _workingPaths.ToList();
            _app.SaveNow();
            MessageBox.Show(this,
                "Extension settings saved. Restart RPM Browser Lite to load or unload extensions.",
                "Extensions", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        private void ApplyTheme()
        {
            bool dark = _app.State.Settings.DarkMode;
            UiTheme.ApplyNativeTree(this, dark);
            UiTheme.ApplyWindowChrome(this, dark);
            _status.ForeColor = dark ? UiTheme.DarkMutedText : Color.FromArgb(90, 90, 90);
        }
    }
}
