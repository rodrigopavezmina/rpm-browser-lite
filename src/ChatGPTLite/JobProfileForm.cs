using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ChatGPTLite
{
    internal sealed class JobProfileForm : Form
    {
        private readonly bool _darkMode;
        private readonly JobApplicationProfile _workingProfile;

        private readonly TextBox _firstName;
        private readonly TextBox _middleName;
        private readonly TextBox _lastName;
        private readonly TextBox _preferredName;
        private readonly TextBox _email;
        private readonly TextBox _phone;
        private readonly TextBox _addressLine1;
        private readonly TextBox _addressLine2;
        private readonly TextBox _city;
        private readonly TextBox _region;
        private readonly TextBox _postalCode;
        private readonly TextBox _country;
        private readonly TextBox _linkedInUrl;
        private readonly TextBox _portfolioUrl;
        private readonly TextBox _githubUrl;
        private readonly TextBox _currentTitle;
        private readonly TextBox _desiredRole;
        private readonly NumericUpDown _yearsExperience;
        private readonly TextBox _workAuthorization;
        private readonly CheckBox _requiresSponsorship;
        private readonly CheckBox _willingToRelocate;
        private readonly TextBox _availableStartDate;
        private readonly TextBox _salaryExpectation;
        private readonly TextBox _skills;
        private readonly TextBox _resumePath;
        private readonly CheckBox _overwriteExisting;
        private readonly ComboBox _workMode;
        private readonly ComboBox _employmentType;
        private readonly DataGridView _experiences;
        private readonly DataGridView _education;

        public JobApplicationProfile Profile { get; private set; }

        public JobProfileForm()
            : this(null, false)
        {
        }

        public JobProfileForm(JobApplicationProfile profile)
            : this(profile, false)
        {
        }

        public JobProfileForm(JobApplicationProfile profile, bool darkMode)
        {
            _darkMode = darkMode;
            _workingProfile = profile == null ? JobApplicationProfile.CreateDefault() : profile.Clone();
            _workingProfile.Normalize();
            Profile = _workingProfile.Clone();

            Text = "Perfil de postulación";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(980, 720);
            MinimumSize = new Size(760, 560);
            ShowInTaskbar = false;

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Point(16, 6);

            TabPage personalPage = new TabPage("Datos personales");
            TableLayoutPanel personalTable = CreateEditorTable();
            int personalRow = 0;
            AddHeading(personalTable, "Identidad y contacto", ref personalRow);
            _firstName = AddTextRow(personalTable, "Nombre *", ref personalRow);
            _middleName = AddTextRow(personalTable, "Segundo nombre", ref personalRow);
            _lastName = AddTextRow(personalTable, "Apellido *", ref personalRow);
            _preferredName = AddTextRow(personalTable, "Nombre preferido", ref personalRow);
            _email = AddTextRow(personalTable, "Correo electrónico *", ref personalRow);
            _phone = AddTextRow(personalTable, "Teléfono con código de país *", ref personalRow);
            AddHeading(personalTable, "Dirección", ref personalRow);
            _addressLine1 = AddTextRow(personalTable, "Dirección / calle", ref personalRow);
            _addressLine2 = AddTextRow(personalTable, "Departamento, oficina u otro", ref personalRow);
            _city = AddTextRow(personalTable, "Ciudad", ref personalRow);
            _region = AddTextRow(personalTable, "Región / estado", ref personalRow);
            _postalCode = AddTextRow(personalTable, "Código postal", ref personalRow);
            _country = AddTextRow(personalTable, "País *", ref personalRow);
            personalPage.Controls.Add(CreateScrollHost(personalTable));

            TabPage preferencesPage = new TabPage("Enlaces y preferencias");
            TableLayoutPanel preferencesTable = CreateEditorTable();
            int preferencesRow = 0;
            AddHeading(preferencesTable, "Enlaces profesionales", ref preferencesRow);
            _linkedInUrl = AddTextRow(preferencesTable, "LinkedIn", ref preferencesRow);
            _portfolioUrl = AddTextRow(preferencesTable, "Portafolio", ref preferencesRow);
            _githubUrl = AddTextRow(preferencesTable, "GitHub", ref preferencesRow);
            AddHeading(preferencesTable, "Preferencias laborales", ref preferencesRow);
            _currentTitle = AddTextRow(preferencesTable, "Cargo actual", ref preferencesRow);
            _desiredRole = AddTextRow(preferencesTable, "Cargo buscado", ref preferencesRow);

            _yearsExperience = new NumericUpDown();
            _yearsExperience.Minimum = 0;
            _yearsExperience.Maximum = 80;
            _yearsExperience.Width = 120;
            _yearsExperience.Anchor = AnchorStyles.Left;
            AddControlRow(preferencesTable, "Años de experiencia", _yearsExperience, ref preferencesRow, 34);

            _workMode = new ComboBox();
            _workMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _workMode.Items.AddRange(new object[] { "Sin preferencia", "Remoto", "Híbrido", "Presencial" });
            _workMode.Dock = DockStyle.Fill;
            AddControlRow(preferencesTable, "Modalidad", _workMode, ref preferencesRow, 34);

            _employmentType = new ComboBox();
            _employmentType.DropDownStyle = ComboBoxStyle.DropDownList;
            _employmentType.Items.AddRange(new object[]
            {
                "Tiempo completo", "Medio tiempo", "Contrato", "Temporal", "Práctica", "Sin preferencia"
            });
            _employmentType.Dock = DockStyle.Fill;
            AddControlRow(preferencesTable, "Tipo de empleo", _employmentType, ref preferencesRow, 34);

            _workAuthorization = AddTextRow(preferencesTable, "Autorización de trabajo", ref preferencesRow);
            _requiresSponsorship = AddCheckRow(preferencesTable,
                "Necesito patrocinio de visa ahora o en el futuro", ref preferencesRow);
            _willingToRelocate = AddCheckRow(preferencesTable,
                "Estoy disponible para reubicarme", ref preferencesRow);
            _availableStartDate = AddTextRow(preferencesTable,
                "Disponible desde (AAAA-MM-DD)", ref preferencesRow);
            _salaryExpectation = AddTextRow(preferencesTable,
                "Expectativa salarial (monto, moneda y período)", ref preferencesRow);

            _skills = new TextBox();
            _skills.Multiline = true;
            _skills.ScrollBars = ScrollBars.Vertical;
            _skills.AcceptsReturn = true;
            _skills.Dock = DockStyle.Fill;
            AddControlRow(preferencesTable, "Habilidades / palabras clave", _skills, ref preferencesRow, 86);

            AddHeading(preferencesTable, "Currículum y comportamiento", ref preferencesRow);
            _resumePath = new TextBox();
            _resumePath.Dock = DockStyle.Fill;
            Button browseResume = new Button();
            browseResume.Text = "Examinar…";
            browseResume.Width = 112;
            browseResume.Dock = DockStyle.Right;
            browseResume.Click += BrowseResumeClicked;
            Panel resumePanel = new Panel();
            resumePanel.Dock = DockStyle.Fill;
            resumePanel.Height = 30;
            resumePanel.Controls.Add(_resumePath);
            resumePanel.Controls.Add(browseResume);
            AddControlRow(preferencesTable, "Archivo de CV *", resumePanel, ref preferencesRow, 36);
            _overwriteExisting = AddCheckRow(preferencesTable,
                "Reemplazar respuestas que el portal ya tenga completas", ref preferencesRow);
            Label overwriteHint = new Label();
            overwriteHint.Text = "Recomendación: mantenlo desmarcado para conservar datos que el portal o tú ya ingresaron.";
            overwriteHint.AutoSize = true;
            overwriteHint.ForeColor = Color.FromArgb(90, 90, 90);
            overwriteHint.Margin = new Padding(6, 0, 6, 10);
            AddWideControl(preferencesTable, overwriteHint, ref preferencesRow, 32);
            preferencesPage.Controls.Add(CreateScrollHost(preferencesTable));

            TabPage experiencePage = new TabPage("Experiencia");
            _experiences = CreateExperiencesGrid();
            experiencePage.Controls.Add(CreateGridHost(
                _experiences,
                "Agrega un empleo por fila. Usa AAAA-MM en las fechas y marca Actual si sigues allí.",
                "Agregar experiencia"));

            TabPage educationPage = new TabPage("Educación");
            _education = CreateEducationGrid();
            educationPage.Controls.Add(CreateGridHost(
                _education,
                "Agrega un estudio por fila. Las fechas son opcionales y usan el formato AAAA-MM.",
                "Agregar educación"));

            tabs.TabPages.Add(personalPage);
            tabs.TabPages.Add(preferencesPage);
            tabs.TabPages.Add(experiencePage);
            tabs.TabPages.Add(educationPage);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 58;
            footer.Padding = new Padding(12, 12, 18, 10);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Right;
            actions.AutoSize = true;
            actions.WrapContents = false;
            actions.FlowDirection = FlowDirection.RightToLeft;

            Button cancel = new Button();
            cancel.Text = "Cancelar";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Width = 105;
            cancel.Height = 32;

            Button save = new Button();
            save.Text = "Guardar perfil";
            save.Width = 125;
            save.Height = 32;
            save.Click += SaveClicked;

            actions.Controls.Add(cancel);
            actions.Controls.Add(save);
            footer.Controls.Add(actions);
            Controls.Add(tabs);
            Controls.Add(footer);

            AcceptButton = save;
            CancelButton = cancel;

            LoadProfile();
            UiTheme.ApplyNativeTree(this, _darkMode);
            ApplyGridTheme(_experiences, _darkMode);
            ApplyGridTheme(_education, _darkMode);
            Shown += delegate { UiTheme.ApplyWindowChrome(this, _darkMode); };
        }

        private void LoadProfile()
        {
            _firstName.Text = _workingProfile.FirstName;
            _middleName.Text = _workingProfile.MiddleName;
            _lastName.Text = _workingProfile.LastName;
            _preferredName.Text = _workingProfile.PreferredName;
            _email.Text = _workingProfile.Email;
            _phone.Text = _workingProfile.Phone;
            _addressLine1.Text = _workingProfile.AddressLine1;
            _addressLine2.Text = _workingProfile.AddressLine2;
            _city.Text = _workingProfile.City;
            _region.Text = _workingProfile.Region;
            _postalCode.Text = _workingProfile.PostalCode;
            _country.Text = _workingProfile.Country;
            _linkedInUrl.Text = _workingProfile.LinkedInUrl;
            _portfolioUrl.Text = _workingProfile.PortfolioUrl;
            _githubUrl.Text = _workingProfile.GitHubUrl;
            _currentTitle.Text = _workingProfile.CurrentTitle;
            _desiredRole.Text = _workingProfile.DesiredRole;
            _yearsExperience.Value = Math.Min(_yearsExperience.Maximum,
                Math.Max(_yearsExperience.Minimum, _workingProfile.YearsExperience));
            SetComboValue(_workMode, _workingProfile.WorkMode, "Sin preferencia");
            SetComboValue(_employmentType, _workingProfile.EmploymentType, "Tiempo completo");
            _workAuthorization.Text = _workingProfile.WorkAuthorization;
            _requiresSponsorship.Checked = _workingProfile.RequiresSponsorship;
            _willingToRelocate.Checked = _workingProfile.WillingToRelocate;
            _availableStartDate.Text = _workingProfile.AvailableStartDate;
            _salaryExpectation.Text = _workingProfile.SalaryExpectation;
            _skills.Text = _workingProfile.Skills;
            _resumePath.Text = _workingProfile.ResumePath;
            _overwriteExisting.Checked = _workingProfile.OverwriteExisting;

            foreach (JobExperience experience in _workingProfile.Experiences)
            {
                _experiences.Rows.Add(
                    experience.Company,
                    experience.Title,
                    experience.Location,
                    experience.StartDate,
                    experience.EndDate,
                    experience.IsCurrent,
                    experience.Description);
            }

            foreach (JobEducation education in _workingProfile.Education)
            {
                _education.Rows.Add(
                    education.Institution,
                    education.Degree,
                    education.FieldOfStudy,
                    education.StartDate,
                    education.EndDate);
            }
        }

        private void BrowseResumeClicked(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Seleccionar currículum";
                dialog.Filter = "Currículum (*.pdf;*.doc;*.docx)|*.pdf;*.doc;*.docx|Todos los archivos (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                string currentPath = _resumePath.Text.Trim();
                if (currentPath.Length > 0)
                {
                    try
                    {
                        string directory = Path.GetDirectoryName(currentPath);
                        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                            dialog.InitialDirectory = directory;
                    }
                    catch
                    {
                    }
                }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _resumePath.Text = dialog.FileName;
            }
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            EndGridEdits(_experiences);
            EndGridEdits(_education);

            JobApplicationProfile candidate = BuildProfile();
            candidate.Normalize();
            List<string> errors = candidate.Validate();
            if (errors.Count > 0)
            {
                MessageBox.Show(
                    BuildValidationMessage(errors),
                    "Revisa el perfil",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Profile = candidate.Clone();
            DialogResult = DialogResult.OK;
            Close();
        }

        private JobApplicationProfile BuildProfile()
        {
            JobApplicationProfile candidate = _workingProfile.Clone();
            candidate.FirstName = _firstName.Text;
            candidate.MiddleName = _middleName.Text;
            candidate.LastName = _lastName.Text;
            candidate.PreferredName = _preferredName.Text;
            candidate.Email = _email.Text;
            candidate.Phone = _phone.Text;
            candidate.AddressLine1 = _addressLine1.Text;
            candidate.AddressLine2 = _addressLine2.Text;
            candidate.City = _city.Text;
            candidate.Region = _region.Text;
            candidate.PostalCode = _postalCode.Text;
            candidate.Country = _country.Text;
            candidate.LinkedInUrl = _linkedInUrl.Text;
            candidate.PortfolioUrl = _portfolioUrl.Text;
            candidate.GitHubUrl = _githubUrl.Text;
            candidate.CurrentTitle = _currentTitle.Text;
            candidate.DesiredRole = _desiredRole.Text;
            candidate.YearsExperience = Decimal.ToInt32(_yearsExperience.Value);
            candidate.WorkMode = Convert.ToString(_workMode.SelectedItem) ?? string.Empty;
            candidate.EmploymentType = Convert.ToString(_employmentType.SelectedItem) ?? string.Empty;
            candidate.WorkAuthorization = _workAuthorization.Text;
            candidate.RequiresSponsorship = _requiresSponsorship.Checked;
            candidate.WillingToRelocate = _willingToRelocate.Checked;
            candidate.AvailableStartDate = _availableStartDate.Text;
            candidate.SalaryExpectation = _salaryExpectation.Text;
            candidate.Skills = _skills.Text;
            candidate.ResumePath = _resumePath.Text;
            candidate.OverwriteExisting = _overwriteExisting.Checked;
            candidate.Experiences = ReadExperiences();
            candidate.Education = ReadEducation();
            return candidate;
        }

        private List<JobExperience> ReadExperiences()
        {
            List<JobExperience> result = new List<JobExperience>();
            foreach (DataGridViewRow row in _experiences.Rows)
            {
                if (row.IsNewRow) continue;
                JobExperience experience = new JobExperience();
                experience.Company = CellText(row, "Company");
                experience.Title = CellText(row, "Title");
                experience.Location = CellText(row, "Location");
                experience.StartDate = CellText(row, "StartDate");
                experience.EndDate = CellText(row, "EndDate");
                experience.IsCurrent = CellBoolean(row, "IsCurrent");
                experience.Description = CellText(row, "Description");
                if (!experience.IsEmpty()) result.Add(experience);
            }
            return result;
        }

        private List<JobEducation> ReadEducation()
        {
            List<JobEducation> result = new List<JobEducation>();
            foreach (DataGridViewRow row in _education.Rows)
            {
                if (row.IsNewRow) continue;
                JobEducation education = new JobEducation();
                education.Institution = CellText(row, "Institution");
                education.Degree = CellText(row, "Degree");
                education.FieldOfStudy = CellText(row, "FieldOfStudy");
                education.StartDate = CellText(row, "StartDate");
                education.EndDate = CellText(row, "EndDate");
                if (!education.IsEmpty()) result.Add(education);
            }
            return result;
        }

        private static string BuildValidationMessage(List<string> errors)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("Corrige lo siguiente antes de guardar:");
            message.AppendLine();
            int limit = Math.Min(errors.Count, 12);
            int index;
            for (index = 0; index < limit; index++)
                message.AppendLine("• " + errors[index]);
            if (errors.Count > limit)
                message.AppendLine("• …y " + (errors.Count - limit) + " problema(s) más.");
            return message.ToString().TrimEnd();
        }

        private static void EndGridEdits(DataGridView grid)
        {
            try
            {
                if (grid.IsCurrentCellInEditMode) grid.EndEdit();
            }
            catch
            {
            }
        }

        private static string CellText(DataGridViewRow row, string columnName)
        {
            object value = row.Cells[columnName].Value;
            return value == null ? string.Empty : Convert.ToString(value);
        }

        private static bool CellBoolean(DataGridViewRow row, string columnName)
        {
            object value = row.Cells[columnName].Value;
            if (value == null) return false;
            if (value is bool) return (bool)value;
            bool parsed;
            return bool.TryParse(Convert.ToString(value), out parsed) && parsed;
        }

        private static TableLayoutPanel CreateEditorTable()
        {
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Top;
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.ColumnCount = 2;
            table.Padding = new Padding(18, 14, 22, 22);
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            return table;
        }

        private static Panel CreateScrollHost(Control content)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.AutoScroll = true;
            panel.Controls.Add(content);
            return panel;
        }

        private static void AddHeading(TableLayoutPanel table, string text, ref int row)
        {
            Label heading = new Label();
            heading.Text = text;
            heading.AutoSize = true;
            heading.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
            heading.Margin = new Padding(3, row == 0 ? 3 : 18, 3, 7);
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(heading, 0, row);
            table.SetColumnSpan(heading, 2);
            row++;
        }

        private static TextBox AddTextRow(TableLayoutPanel table, string label, ref int row)
        {
            TextBox textBox = new TextBox();
            textBox.Dock = DockStyle.Fill;
            AddControlRow(table, label, textBox, ref row, 34);
            return textBox;
        }

        private static CheckBox AddCheckRow(TableLayoutPanel table, string text, ref int row)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = text;
            checkBox.AutoSize = true;
            checkBox.Margin = new Padding(6, 7, 6, 7);
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(checkBox, 1, row);
            row++;
            return checkBox;
        }

        private static void AddControlRow(
            TableLayoutPanel table,
            string labelText,
            Control control,
            ref int row,
            int height)
        {
            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(3, 7, 12, 3);
            control.Margin = new Padding(3, 3, 3, 5);
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            table.Controls.Add(label, 0, row);
            table.Controls.Add(control, 1, row);
            row++;
        }

        private static void AddWideControl(TableLayoutPanel table, Control control, ref int row, int height)
        {
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            table.Controls.Add(control, 1, row);
            row++;
        }

        private static DataGridView CreateExperiencesGrid()
        {
            DataGridView grid = CreateGrid();
            AddTextColumn(grid, "Company", "Empresa *", 125, 14f);
            AddTextColumn(grid, "Title", "Cargo *", 125, 14f);
            AddTextColumn(grid, "Location", "Ubicación", 100, 10f);
            AddTextColumn(grid, "StartDate", "Inicio *", 82, 8f);
            AddTextColumn(grid, "EndDate", "Término", 82, 8f);

            DataGridViewCheckBoxColumn current = new DataGridViewCheckBoxColumn();
            current.Name = "IsCurrent";
            current.HeaderText = "Actual";
            current.Width = 58;
            current.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            grid.Columns.Add(current);

            AddTextColumn(grid, "Description", "Logros y responsabilidades", 220, 28f);
            return grid;
        }

        private static DataGridView CreateEducationGrid()
        {
            DataGridView grid = CreateGrid();
            AddTextColumn(grid, "Institution", "Institución *", 170, 25f);
            AddTextColumn(grid, "Degree", "Título / grado *", 170, 25f);
            AddTextColumn(grid, "FieldOfStudy", "Área de estudio", 170, 25f);
            AddTextColumn(grid, "StartDate", "Inicio", 90, 12f);
            AddTextColumn(grid, "EndDate", "Término", 90, 12f);
            return grid;
        }

        private static DataGridView CreateGrid()
        {
            DataGridView grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = true;
            grid.AllowUserToDeleteRows = true;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.MultiSelect = true;
            grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = SystemColors.Window;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            grid.DataError += delegate { };
            return grid;
        }

        private static void AddTextColumn(
            DataGridView grid,
            string name,
            string header,
            int minimumWidth,
            float fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.MinimumWidth = minimumWidth;
            column.FillWeight = fillWeight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(column);
        }

        private static Control CreateGridHost(DataGridView grid, string instructions, string addText)
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.Padding = new Padding(15);

            Panel commands = new Panel();
            commands.Dock = DockStyle.Top;
            commands.Height = 70;

            Label label = new Label();
            label.Text = instructions;
            label.AutoSize = false;
            label.Height = 36;
            label.Dock = DockStyle.Top;

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 34;
            buttons.WrapContents = false;

            Button add = new Button();
            add.Text = addText;
            add.AutoSize = true;
            add.Height = 29;
            add.Click += delegate
            {
                int index = grid.Rows.Add();
                if (index >= 0 && grid.Columns.Count > 0)
                    grid.CurrentCell = grid.Rows[index].Cells[0];
                grid.Focus();
            };

            Button remove = new Button();
            remove.Text = "Eliminar fila seleccionada";
            remove.AutoSize = true;
            remove.Height = 29;
            remove.Click += delegate { RemoveSelectedRows(grid); };

            buttons.Controls.Add(add);
            buttons.Controls.Add(remove);
            commands.Controls.Add(label);
            commands.Controls.Add(buttons);
            host.Controls.Add(grid);
            host.Controls.Add(commands);
            return host;
        }

        private static void RemoveSelectedRows(DataGridView grid)
        {
            HashSet<DataGridViewRow> selected = new HashSet<DataGridViewRow>();
            foreach (DataGridViewRow row in grid.SelectedRows)
            {
                if (!row.IsNewRow) selected.Add(row);
            }
            foreach (DataGridViewCell cell in grid.SelectedCells)
            {
                if (!cell.OwningRow.IsNewRow) selected.Add(cell.OwningRow);
            }
            if (selected.Count == 0 && grid.CurrentRow != null && !grid.CurrentRow.IsNewRow)
                selected.Add(grid.CurrentRow);
            foreach (DataGridViewRow row in selected)
                grid.Rows.Remove(row);
        }

        private static void SetComboValue(ComboBox combo, string value, string fallback)
        {
            string selected = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            int index;
            for (index = 0; index < combo.Items.Count; index++)
            {
                if (string.Equals(Convert.ToString(combo.Items[index]), selected,
                    StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = index;
                    return;
                }
            }
            combo.Items.Add(selected);
            combo.SelectedIndex = combo.Items.Count - 1;
        }

        private static void ApplyGridTheme(DataGridView grid, bool darkMode)
        {
            Color background = darkMode ? UiTheme.DarkSurface : Color.White;
            Color input = darkMode ? UiTheme.DarkInput : Color.White;
            Color text = darkMode ? UiTheme.DarkText : UiTheme.LightText;
            Color header = darkMode ? Color.FromArgb(49, 58, 68) : Color.FromArgb(232, 236, 241);
            Color selection = darkMode ? Color.FromArgb(45, 88, 126) : Color.FromArgb(204, 228, 250);

            grid.BackgroundColor = background;
            grid.GridColor = darkMode ? Color.FromArgb(72, 82, 94) : Color.FromArgb(210, 215, 221);
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = header;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = text;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = header;
            grid.DefaultCellStyle.BackColor = input;
            grid.DefaultCellStyle.ForeColor = text;
            grid.DefaultCellStyle.SelectionBackColor = selection;
            grid.DefaultCellStyle.SelectionForeColor = text;
            grid.RowsDefaultCellStyle.BackColor = input;
            grid.AlternatingRowsDefaultCellStyle.BackColor = darkMode
                ? Color.FromArgb(37, 44, 52)
                : Color.FromArgb(248, 249, 250);
        }
    }
}
