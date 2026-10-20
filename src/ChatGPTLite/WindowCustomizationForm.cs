using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ChatGPTLite
{
    internal sealed class WindowCustomizationForm : Form
    {
        private readonly AppController _app;
        private readonly BrowserWindow _window;
        private readonly ComboBox _identifier;
        private readonly TextBox _title;
        private readonly ComboBox _glyph;
        private readonly CheckBox _showNotes;
        private readonly TextBox _category;
        private readonly Panel _iconPreview;
        private readonly Label _captionPreview;
        private readonly Label _iconSourceLabel;
        private Color _iconColor;
        private bool _useFavicon;
        private string _selectedCustomIconPath;

        public WindowCustomizationForm(AppController app, BrowserWindow window)
        {
            _app = app;
            _window = window;
            Text = "Título, identificador e icono";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(610, 505);
            Font = new Font("Segoe UI", 9f);

            Label explanation = new Label();
            explanation.Text = "The ID is optional and appears at the end of the window title. Type 7, 07 or TD-D07; all become TD-D07. Suggestions run from TD-D01 to TD-D50.";
            explanation.Location = new Point(20, 19);
            explanation.Size = new Size(565, 42);

            Label idLabel = LabelAt("Identificador:", 22, 88, 115);
            _identifier = new ComboBox();
            _identifier.Location = new Point(140, 84);
            _identifier.Width = 210;
            _identifier.DropDownStyle = ComboBoxStyle.DropDown;
            foreach (string identifier in _app.State.KnownIdentifiers.OrderBy(delegate(string value) { return value; }))
                _identifier.Items.Add(identifier);
            _identifier.Text = window.Record.Identifier ?? string.Empty;
            _identifier.Leave += delegate { _identifier.Text = _app.NormalizeIdentifierInput(_identifier.Text); };
            _identifier.KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.KeyCode == Keys.Enter)
                {
                    _identifier.Text = _app.NormalizeIdentifierInput(_identifier.Text);
                    _title.Focus();
                    args.SuppressKeyPress = true;
                    args.Handled = true;
                }
            };
            Label optional = LabelAt("Opcional y repetible", 363, 88, 160);
            optional.ForeColor = Color.FromArgb(95, 95, 95);

            Label titleLabel = LabelAt("Título:", 22, 129, 115);
            _title = new TextBox();
            _title.Location = new Point(140, 125);
            _title.Width = 425;
            _title.Text = window.Record.CustomTitle ?? string.Empty;

            Label titleHint = LabelAt("Leave it empty to keep the page title.", 140, 152, 410);
            titleHint.ForeColor = Color.FromArgb(95, 95, 95);

            Label iconLabel = LabelAt("Icono:", 22, 203, 115);
            _iconPreview = new Panel();
            _iconPreview.Location = new Point(140, 190);
            _iconPreview.Size = new Size(42, 42);
            _iconPreview.Paint += IconPreviewPaint;

            _glyph = new ComboBox();
            _glyph.Location = new Point(194, 198);
            _glyph.Width = 106;
            _glyph.DropDownStyle = ComboBoxStyle.DropDown;
            foreach (string glyph in IconVisuals.GlyphChoices)
                _glyph.Items.Add(glyph);
            _glyph.Text = IconVisuals.GetGlyph(window.Record);
            _glyph.TextChanged += delegate { UpdatePreview(); };

            Button colorButton = new Button();
            colorButton.Text = "Usar color…";
            colorButton.AutoSize = true;
            colorButton.Location = new Point(314, 196);
            colorButton.Click += SelectColor;

            Button faviconButton = new Button();
            faviconButton.Text = "Favicon automatico";
            faviconButton.AutoSize = true;
            faviconButton.Location = new Point(194, 236);
            faviconButton.Click += SelectAutomaticFavicon;

            Button customIconButton = new Button();
            customIconButton.Text = "Cargar ICO/PNG...";
            customIconButton.AutoSize = true;
            customIconButton.Location = new Point(344, 236);
            customIconButton.Click += SelectCustomIcon;

            _iconSourceLabel = LabelAt(string.Empty, 140, 267, 425);
            _iconSourceLabel.ForeColor = Color.FromArgb(95, 95, 95);

            _showNotes = new CheckBox();
            _showNotes.Text = "Mostrar notas adjuntas al lado derecho de esta ventana";
            _showNotes.AutoSize = true;
            _showNotes.Location = new Point(140, 294);
            _showNotes.Checked = window.Record.NotesVisible;

            Label categoryLabel = LabelAt("Categoría:", 22, 291, 115);
            _category = new TextBox();
            _category.Location = new Point(140, 318);
            _category.Width = 425;
            _category.Text = window.Record.Category ?? string.Empty;

            Panel divider = new Panel();
            divider.BackColor = Color.FromArgb(200, 200, 200);
            divider.Location = new Point(20, 368);
            divider.Size = new Size(565, 1);

            _captionPreview = new Label();
            _captionPreview.BorderStyle = BorderStyle.FixedSingle;
            _captionPreview.BackColor = Color.FromArgb(249, 249, 249);
            _captionPreview.Location = new Point(20, 384);
            _captionPreview.Size = new Size(565, 28);
            _captionPreview.TextAlign = ContentAlignment.MiddleLeft;
            _captionPreview.Padding = new Padding(7, 0, 7, 0);

            Button reset = new Button();
            reset.Text = "Restablecer";
            reset.AutoSize = true;
            reset.Location = new Point(20, 446);
            reset.Click += ResetClicked;

            Button save = new Button();
            save.Text = "Aceptar";
            save.Width = 88;
            save.Height = 30;
            save.Location = new Point(397, 443);
            save.Click += SaveClicked;

            Button cancel = new Button();
            cancel.Text = "Cancelar";
            cancel.Width = 88;
            cancel.Height = 30;
            cancel.Location = new Point(497, 443);
            cancel.DialogResult = DialogResult.Cancel;

            Controls.Add(explanation);
            Controls.Add(idLabel);
            Controls.Add(_identifier);
            Controls.Add(optional);
            Controls.Add(titleLabel);
            Controls.Add(_title);
            Controls.Add(titleHint);
            Controls.Add(iconLabel);
            Controls.Add(_iconPreview);
            Controls.Add(_glyph);
            Controls.Add(colorButton);
            Controls.Add(faviconButton);
            Controls.Add(customIconButton);
            Controls.Add(_iconSourceLabel);
            Controls.Add(_showNotes);
            Controls.Add(categoryLabel);
            Controls.Add(_category);
            Controls.Add(divider);
            Controls.Add(_captionPreview);
            Controls.Add(reset);
            Controls.Add(save);
            Controls.Add(cancel);

            AcceptButton = save;
            CancelButton = cancel;
            _iconColor = IconVisuals.GetColor(window.Record);
            _useFavicon = window.Record.UseFavicon != false;
            _selectedCustomIconPath = window.Record.CustomIconPath ?? string.Empty;
            _identifier.TextChanged += delegate { UpdatePreview(); };
            _title.TextChanged += delegate { UpdatePreview(); };
            UpdateIconSourceLabel();
            UpdatePreview();
        }

        private void SelectColor(object sender, EventArgs e)
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = _iconColor;
                dialog.FullOpen = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _iconColor = dialog.Color;
                    UpdatePreview();
                }
            }
        }

        private void SelectAutomaticFavicon(object sender, EventArgs e)
        {
            _useFavicon = true;
            _selectedCustomIconPath = string.Empty;
            UpdateIconSourceLabel();
            UpdatePreview();
        }

        private void SelectCustomIcon(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Seleccionar icono personalizado";
                dialog.Filter = "Iconos y PNG|*.ico;*.png|Iconos ICO|*.ico|Imagen PNG|*.png";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _useFavicon = false;
                    _selectedCustomIconPath = dialog.FileName;
                    UpdateIconSourceLabel();
                    UpdatePreview();
                }
            }
        }

        private void UpdateIconSourceLabel()
        {
            if (_useFavicon)
            {
                _iconSourceLabel.Text = "Origen: favicon automatico de la pagina";
                return;
            }

            string path = _selectedCustomIconPath;
            if (string.IsNullOrWhiteSpace(path))
                path = _window.Record.CustomIconPath;
            _iconSourceLabel.Text = string.IsNullOrWhiteSpace(path)
                ? "Origen: icono del navegador (selecciona un ICO o PNG)"
                : "Origen: " + Path.GetFileName(path);
        }

        private void ResetClicked(object sender, EventArgs e)
        {
            _identifier.Text = string.Empty;
            _title.Text = string.Empty;
            _glyph.Text = "✦";
            _iconColor = Color.FromArgb(75, 115, 184);
            _useFavicon = true;
            _selectedCustomIconPath = string.Empty;
            _showNotes.Checked = false;
            _category.Text = string.Empty;
            UpdateIconSourceLabel();
            UpdatePreview();
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            _window.Record.Identifier = _app.NormalizeIdentifierInput(_identifier.Text);
            _window.Record.CustomTitle = (_title.Text ?? string.Empty).Trim();
            _window.Record.IconGlyph = IconVisuals.GetGlyphFromText(_glyph.Text);
            _window.Record.IconColorArgb = _iconColor.ToArgb();
            _window.Record.NotesVisible = _showNotes.Checked;
            _window.Record.Category = (_category.Text ?? string.Empty).Trim();
            _window.Record.UseFavicon = _useFavicon;
            if (_useFavicon)
            {
                _window.Record.CustomIconPath = string.Empty;
            }
            else if (!string.IsNullOrWhiteSpace(_selectedCustomIconPath))
            {
                try
                {
                    if (!string.Equals(_selectedCustomIconPath, _window.Record.CustomIconPath, StringComparison.OrdinalIgnoreCase) ||
                        !File.Exists(_window.Record.CustomIconPath))
                        _window.Record.CustomIconPath = _app.StoreCustomIcon(_window.Record, _selectedCustomIconPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, AppController.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            _app.RememberIdentifier(_window.Record.Identifier);
            _window.ApplyPresentation();
            _app.RememberDomainProfile(_window.Record);
            _app.WindowPresentationChanged();
            Close();
        }

        private void UpdatePreview()
        {
            _iconPreview.Invalidate();
            string title = string.IsNullOrWhiteSpace(_title.Text)
                ? _window.Record.DisplayTitle(_window.PageTitle)
                : _title.Text.Trim();
            string identifier = (_identifier.Text ?? string.Empty).Trim();
            _captionPreview.Text = ComposeCaption(title, identifier);
        }

        private void IconPreviewPaint(object sender, PaintEventArgs e)
        {
            string imagePath = _useFavicon
                ? _window.Record.FaviconPath
                : (_selectedCustomIconPath ?? _window.Record.CustomIconPath);
            if (!IconVisuals.DrawImage(e.Graphics, imagePath, new Rectangle(2, 2, 38, 38)))
                IconVisuals.DrawBadge(e.Graphics, new Rectangle(2, 2, 38, 38), IconVisuals.GetGlyphFromText(_glyph.Text), _iconColor);
        }

        private static string ComposeCaption(string title, string identifier)
        {
            string caption = (string.IsNullOrWhiteSpace(title) ? "Browser window" : title.Trim()) + " · " + AppController.ProductName;
            if (!string.IsNullOrWhiteSpace(identifier))
                caption += " · " + identifier.Trim();
            return caption;
        }

        private static Label LabelAt(string text, int x, int y, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Width = width;
            label.Height = 24;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }
    }
}
