using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace ChatGPTLite
{
    internal sealed class SalesCopilotForm : Form
    {
        private readonly SalesWorkspace _workspace;
        private DataGridView _grid;
        private TextBox _search;
        private RichTextBox _detail;
        private Label _totalLabel;
        private Label _atRiskLabel;
        private Label _nextLabel;
        private ListBox _priorityList;
        private Opportunity _selected;

        public SalesCopilotForm()
        {
            _workspace = SalesStore.Load();
            Text = "B2B Copilot Lite";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1050, 680);
            Size = new Size(1300, 820);
            BackColor = Color.FromArgb(244, 247, 250);
            Font = new Font("Segoe UI", 9F);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Color.FromArgb(20, 35, 55), Padding = new Padding(24, 13, 24, 12) };
            Label title = new Label { AutoSize = true, Text = "B2B COPILOT  LITE", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 18F), Location = new Point(24, 12) };
            Label subtitle = new Label { AutoSize = true, Text = "Disciplina comercial · Solución defendible · Ejecución coordinada", ForeColor = Color.FromArgb(184, 205, 222), Location = new Point(27, 45) };
            header.Controls.Add(title); header.Controls.Add(subtitle);
            Button add = HeaderButton("+ Nueva oportunidad", 160); add.Click += delegate { CreateOpportunity(); };
            Button edit = HeaderButton("Editar ficha", 108); edit.Click += delegate { EditSelected(); };
            Button war = HeaderButton("WarRoom", 90); war.Click += delegate { CreateWarRoom(); };
            Button push = HeaderButton("Push Push", 90); push.Click += delegate { CreatePushPush(); };
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 470, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent };
            actions.Controls.Add(add); actions.Controls.Add(edit); actions.Controls.Add(war); actions.Controls.Add(push); header.Controls.Add(actions);
            Controls.Add(header);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(18, 8) };
            tabs.TabPages.Add(BuildDashboard());
            tabs.TabPages.Add(BuildOpportunities());
            tabs.TabPages.Add(BuildMethod());
            Controls.Add(tabs);

            _grid.SelectionChanged += delegate { SelectFromGrid(); };
            RefreshAll();
        }

        private Button HeaderButton(string text, int width)
        {
            return new Button { Text = text, Width = width, Height = 36, Margin = new Padding(7, 6, 0, 0), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(46, 90, 125), ForeColor = Color.White, FlatAppearance = { BorderSize = 0 }, Cursor = Cursors.Hand };
        }

        private TabPage BuildDashboard()
        {
            TabPage page = new TabPage("Resumen ejecutivo") { BackColor = BackColor, Padding = new Padding(24) };
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            FlowLayoutPanel cards = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 8, 0, 8) };
            _totalLabel = MetricCard("OPORTUNIDADES ACTIVAS", "0", Color.FromArgb(36, 91, 135));
            _atRiskLabel = MetricCard("CON RIESGO O BLOQUEO", "0", Color.FromArgb(178, 83, 55));
            _nextLabel = MetricCard("HITOS PRÓXIMOS (7 DÍAS)", "0", Color.FromArgb(45, 130, 105));
            cards.Controls.Add(_totalLabel.Parent); cards.Controls.Add(_atRiskLabel.Parent); cards.Controls.Add(_nextLabel.Parent);
            root.Controls.Add(cards, 0, 0);
            root.Controls.Add(new Label { Text = "PRIORIDADES Y PRÓXIMAS ACCIONES", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 10F), ForeColor = Color.FromArgb(59, 74, 89) }, 0, 1);
            _priorityList = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Color.White, Font = new Font("Segoe UI", 10F), IntegralHeight = false, Padding = new Padding(12) };
            _priorityList.DoubleClick += delegate { JumpToSelectedPriority(); };
            root.Controls.Add(_priorityList, 0, 2); page.Controls.Add(root); return page;
        }

        private Label MetricCard(string heading, string value, Color color)
        {
            Panel card = new Panel { Width = 260, Height = 82, BackColor = Color.White, Margin = new Padding(0, 0, 16, 0), Padding = new Padding(16, 12, 12, 8) };
            Panel strip = new Panel { Dock = DockStyle.Left, Width = 5, BackColor = color }; card.Controls.Add(strip);
            Label headingLabel = new Label { Text = heading, Font = new Font("Segoe UI Semibold", 8F), ForeColor = Color.FromArgb(100, 112, 124), AutoSize = true, Location = new Point(18, 12) };
            Label valueLabel = new Label { Text = value, Font = new Font("Segoe UI Semibold", 23F), ForeColor = color, AutoSize = true, Location = new Point(17, 32) };
            card.Controls.Add(headingLabel); card.Controls.Add(valueLabel); return valueLabel;
        }

        private TabPage BuildOpportunities()
        {
            TabPage page = new TabPage("Oportunidades") { BackColor = BackColor, Padding = new Padding(16) };
            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 800, BackColor = BackColor };
            Panel left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 10, 0) };
            Panel filter = new Panel { Dock = DockStyle.Top, Height = 44 };
            filter.Controls.Add(new Label { Text = "Buscar", AutoSize = true, Location = new Point(0, 12), Font = new Font("Segoe UI Semibold", 9F) });
            _search = new TextBox { Location = new Point(56, 8), Width = 300 };
            _search.TextChanged += delegate { RefreshGrid(); }; filter.Controls.Add(_search);
            Button clear = new Button { Text = "Limpiar", Location = new Point(366, 7), Width = 74, Height = 27 }; clear.Click += delegate { _search.Clear(); }; filter.Controls.Add(clear);
            left.Controls.Add(filter);
            _grid = new DataGridView { Dock = DockStyle.Fill, Top = 44, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, RowHeadersVisible = false, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells };
            _grid.Columns.Add(Column("Oportunidad", 190)); _grid.Columns.Add(Column("Cliente", 140)); _grid.Columns.Add(Column("Etapa", 110)); _grid.Columns.Add(Column("Salud", 95)); _grid.Columns.Add(Column("Próximo hito", 180)); _grid.Columns.Add(Column("Fecha", 84)); _grid.Columns.Add(Column("Responsable", 110));
            left.Controls.Add(_grid); split.Panel1.Controls.Add(left);
            Panel right = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16) };
            right.Controls.Add(new Label { Text = "FICHA DE OPORTUNIDAD", Dock = DockStyle.Top, Height = 26, Font = new Font("Segoe UI Semibold", 10F), ForeColor = Color.FromArgb(42, 67, 91) });
            _detail = new RichTextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Color.White, ReadOnly = true, Font = new Font("Segoe UI", 9.5F), DetectUrls = false };
            right.Controls.Add(_detail); split.Panel2.Controls.Add(right); page.Controls.Add(split); return page;
        }

        private DataGridViewTextBoxColumn Column(string header, int width)
        {
            return new DataGridViewTextBoxColumn { HeaderText = header, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable };
        }

        private TabPage BuildMethod()
        {
            TabPage page = new TabPage("Método de trabajo") { BackColor = Color.White, Padding = new Padding(30) };
            RichTextBox guide = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.White, Font = new Font("Segoe UI", 10F), DetectUrls = false };
            guide.Text = "CÓMO USA ESTE COPILOTO\n\n" +
                "1. Descubre antes de diseñar. Registra el problema operativo, usuarios, impacto, decisores, presupuesto, plazos y criterios de éxito. Separa hechos, supuestos y brechas.\n\n" +
                "2. Estructura una solución defendible. Organiza especialidades, arquitectura, alcance, interfaces, exclusiones, riesgos, mitigaciones, costos y plan de entrega.\n\n" +
                "3. Mantén disciplina. Pipeline, valor de propuesta, booking, contrato adjudicado e ingreso son campos distintos. La probabilidad requiere evidencia.\n\n" +
                "4. Usa WarRoom cuando exista urgencia o bloqueo. Cierra con acción, responsable, fecha, apoyo y escalamiento.\n\n" +
                "5. Usa Push Push para cada seguimiento: contexto, acción solicitada, responsable, fecha límite y próximo paso.\n\n" +
                "La aplicación no inventa cifras, compromisos, aprobaciones ni capacidades técnicas. Los campos vacíos son información aún por confirmar.";
            page.Controls.Add(guide); return page;
        }

        private void RefreshAll()
        {
            RefreshGrid(); RefreshDashboard();
        }

        private void RefreshGrid()
        {
            string query = _search == null ? string.Empty : _search.Text.Trim().ToLowerInvariant();
            IEnumerable<Opportunity> results = _workspace.Opportunities.Where(delegate(Opportunity o) { return o != null; });
            if (query.Length > 0)
                results = results.Where(delegate(Opportunity o) { return SearchText(o).Contains(query); });
            List<Opportunity> ordered = results.OrderBy(delegate(Opportunity o) { return o.NextMilestoneDate ?? DateTime.MaxValue; }).ThenBy(delegate(Opportunity o) { return o.Name; }).ToList();
            _grid.Rows.Clear();
            foreach (Opportunity opportunity in ordered)
            {
                int index = _grid.Rows.Add(opportunity.Name, opportunity.Customer, opportunity.Stage, opportunity.Health, opportunity.NextMilestone, opportunity.NextMilestoneDate.HasValue ? opportunity.NextMilestoneDate.Value.ToString("dd MMM") : "", opportunity.Owner);
                _grid.Rows[index].Tag = opportunity;
            }
            if (_grid.Rows.Count > 0) _grid.Rows[0].Selected = true;
            else { _selected = null; _detail.Clear(); }
        }

        private static string SearchText(Opportunity o)
        {
            return string.Join(" ", new[] { o.Name, o.Customer, o.Industry, o.Stage, o.Health, o.Owner, o.NextMilestone, o.Blocker }.Where(delegate(string v) { return v != null; })).ToLowerInvariant();
        }

        private void RefreshDashboard()
        {
            List<Opportunity> active = _workspace.Opportunities.Where(delegate(Opportunity o) { return o != null && o.Stage != "Adjudicada" && o.Stage != "Perdida" && o.Stage != "En pausa"; }).ToList();
            int risk = active.Count(delegate(Opportunity o) { return string.Equals(o.Health, "En riesgo", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(o.Blocker); });
            DateTime limit = DateTime.Today.AddDays(7);
            int next = active.Count(delegate(Opportunity o) { return o.NextMilestoneDate.HasValue && o.NextMilestoneDate.Value.Date >= DateTime.Today && o.NextMilestoneDate.Value.Date <= limit; });
            _totalLabel.Text = active.Count.ToString(); _atRiskLabel.Text = risk.ToString(); _nextLabel.Text = next.ToString();
            _priorityList.Items.Clear();
            foreach (Opportunity o in active.OrderBy(delegate(Opportunity x) { return Priority(x); }).ThenBy(delegate(Opportunity x) { return x.NextMilestoneDate ?? DateTime.MaxValue; }))
            {
                string due = o.NextMilestoneDate.HasValue ? o.NextMilestoneDate.Value.ToString("dd MMM") : "fecha pendiente";
                string text = (string.IsNullOrWhiteSpace(o.Name) ? "Oportunidad sin nombre" : o.Name) + " · " + (string.IsNullOrWhiteSpace(o.NextMilestone) ? "Definir próximo hito" : o.NextMilestone) + " · " + due;
                _priorityList.Items.Add(new PriorityItem { Text = text, Opportunity = o });
            }
            if (_priorityList.Items.Count == 0) _priorityList.Items.Add("Aún no hay oportunidades activas. Crea una ficha para comenzar el diagnóstico.");
        }

        private static int Priority(Opportunity o)
        {
            if (!string.IsNullOrWhiteSpace(o.Blocker) || string.Equals(o.Health, "En riesgo", StringComparison.OrdinalIgnoreCase)) return 0;
            if (o.NextMilestoneDate.HasValue && o.NextMilestoneDate.Value.Date <= DateTime.Today.AddDays(7)) return 1;
            return 2;
        }

        private void SelectFromGrid()
        {
            if (_grid.SelectedRows.Count == 0) return;
            _selected = _grid.SelectedRows[0].Tag as Opportunity;
            if (_selected != null) ShowDetail(_selected);
        }

        private void ShowDetail(Opportunity o)
        {
            StringBuilder text = new StringBuilder();
            Add(text, "OPORTUNIDAD", o.Name); Add(text, "CLIENTE", o.Customer); Add(text, "ETAPA / SALUD", Join(o.Stage, o.Health)); Add(text, "RESPONSABLE", o.Owner); Add(text, "PRÓXIMO HITO", Join(o.NextMilestone, o.NextMilestoneDate.HasValue ? o.NextMilestoneDate.Value.ToString("dd MMMM yyyy") : string.Empty)); Add(text, "BLOQUEO", o.Blocker);
            Add(text, "PROBLEMA OPERATIVO", o.OperationalProblem); Add(text, "HECHOS CONFIRMADOS", o.ConfirmedFacts); Add(text, "SUPUESTOS", o.Assumptions); Add(text, "INFORMACIÓN FALTANTE", o.MissingInformation);
            Add(text, "SOLUCIÓN PROPUESTA", o.Solution); Add(text, "RIESGOS / MITIGACIONES", Join(o.Risks, o.Mitigations)); Add(text, "TRASPASO / LECCIONES", Join(o.HandoverNotes, o.LessonsLearned));
            _detail.Text = text.ToString();
        }

        private static string Join(string first, string second) { return string.Join("\r\n", new[] { first, second }.Where(delegate(string x) { return !string.IsNullOrWhiteSpace(x); })); }
        private static void Add(StringBuilder text, string heading, string value) { if (!string.IsNullOrWhiteSpace(value)) text.AppendLine(heading).AppendLine(value.Trim()).AppendLine(); }

        private void CreateOpportunity()
        {
            Opportunity opportunity = Opportunity.Create();
            using (OpportunityEditor editor = new OpportunityEditor(opportunity))
            {
                if (editor.ShowDialog(this) != DialogResult.OK) return;
                _workspace.Opportunities.Add(opportunity); SaveAndRefresh();
            }
        }

        private void EditSelected()
        {
            if (_selected == null) { MessageBox.Show("Selecciona una oportunidad para editarla.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using (OpportunityEditor editor = new OpportunityEditor(_selected))
            {
                if (editor.ShowDialog(this) == DialogResult.OK) SaveAndRefresh();
            }
        }

        private void CreatePushPush()
        {
            if (_selected == null) { MessageBox.Show("Selecciona una oportunidad para preparar el seguimiento.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string deadline = _selected.NextMilestoneDate.HasValue ? _selected.NextMilestoneDate.Value.ToString("dd MMMM yyyy") : "por confirmar";
            string action = !string.IsNullOrWhiteSpace(_selected.NextMilestone) ? _selected.NextMilestone : (!string.IsNullOrWhiteSpace(_selected.MissingInformation) ? "Confirmar la información pendiente: " + _selected.MissingInformation : "Confirmar la acción necesaria para avanzar");
            string message = "Asunto: Seguimiento · " + (_selected.Name.Length == 0 ? "Oportunidad" : _selected.Name) + "\r\n\r\n" +
                "Contexto: estamos avanzando la oportunidad " + (string.IsNullOrWhiteSpace(_selected.Name) ? "sin nombre" : _selected.Name) + " para " + (string.IsNullOrWhiteSpace(_selected.Customer) ? "cliente por confirmar" : _selected.Customer) + ".\r\n\r\n" +
                "Acción solicitada: " + action + ".\r\n" +
                "Responsable: " + (string.IsNullOrWhiteSpace(_selected.Owner) ? "por definir" : _selected.Owner) + ".\r\n" +
                "Fecha límite: " + deadline + ".\r\n" +
                "Próximo paso: confirmar avance, brechas o necesidad de apoyo para actualizar el hito y escalar a tiempo si cambia el compromiso.";
            ShowTextDialog("Mensaje Push Push", "Revisa el mensaje antes de enviarlo. Los campos no confirmados se declaran como pendientes.", message, true);
        }

        private void CreateWarRoom()
        {
            if (_selected == null) { MessageBox.Show("Selecciona una oportunidad para abrir una WarRoom.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            WarRoomSession session = new WarRoomSession { Id = Guid.NewGuid().ToString("N"), OpportunityId = _selected.Id, CreatedUtc = DateTime.UtcNow, Objective = _selected.NextMilestone, Deadline = _selected.NextMilestoneDate.HasValue ? _selected.NextMilestoneDate.Value.ToString("dd MMMM yyyy") : string.Empty, ConfirmedFacts = _selected.ConfirmedFacts, Gaps = _selected.MissingInformation, PendingDecisions = _selected.Blocker };
            using (WarRoomEditor editor = new WarRoomEditor(session, _selected.Name))
            {
                if (editor.ShowDialog(this) == DialogResult.OK)
                {
                    _workspace.WarRooms.Add(session); SalesStore.Save(_workspace);
                    ShowTextDialog("Resumen ejecutivo WarRoom", "Copia y distribuye este resumen para alinear al equipo.", BuildWarRoomSummary(session), true);
                }
            }
        }

        private static string BuildWarRoomSummary(WarRoomSession session)
        {
            return "WARROOM · RESUMEN EJECUTIVO\r\n\r\nObjetivo: " + Value(session.Objective) + "\r\nFecha límite: " + Value(session.Deadline) + "\r\nHechos confirmados: " + Value(session.ConfirmedFacts) + "\r\nBrechas: " + Value(session.Gaps) + "\r\nDecisiones pendientes: " + Value(session.PendingDecisions) + "\r\n\r\nAcción siguiente: " + Value(session.NextAction) + "\r\nResponsable: " + Value(session.Responsible) + "\r\nFecha: " + Value(session.DueDate) + "\r\nApoyo requerido: " + Value(session.SupportRequired) + "\r\nEscalamiento: " + Value(session.EscalationPath) + (string.IsNullOrWhiteSpace(session.ExecutiveSummary) ? string.Empty : "\r\n\r\nNotas: " + session.ExecutiveSummary);
        }

        private static string Value(string value) { return string.IsNullOrWhiteSpace(value) ? "por confirmar" : value.Trim(); }

        private void ShowTextDialog(string title, string hint, string value, bool copy)
        {
            Form dialog = new Form { Text = title, StartPosition = FormStartPosition.CenterParent, Size = new Size(720, 540), MinimizeBox = false, MaximizeBox = false };
            Label description = new Label { Text = hint, Dock = DockStyle.Top, Height = 40, Padding = new Padding(14, 12, 14, 0) };
            RichTextBox box = new RichTextBox { Dock = DockStyle.Fill, Text = value, Font = new Font("Segoe UI", 10F), BorderStyle = BorderStyle.FixedSingle };
            FlowLayoutPanel controls = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            Button close = new Button { Text = "Cerrar", DialogResult = DialogResult.OK, Width = 82 };
            controls.Controls.Add(close);
            if (copy) { Button copyButton = new Button { Text = "Copiar", Width = 82 }; copyButton.Click += delegate { Clipboard.SetText(box.Text); copyButton.Text = "Copiado"; }; controls.Controls.Add(copyButton); }
            dialog.Controls.Add(box); dialog.Controls.Add(controls); dialog.Controls.Add(description); dialog.AcceptButton = close; dialog.ShowDialog(this);
        }

        private void SaveAndRefresh()
        {
            if (_selected != null) _selected.UpdatedUtc = DateTime.UtcNow;
            SalesStore.Save(_workspace); RefreshAll();
        }

        private void JumpToSelectedPriority()
        {
            PriorityItem item = _priorityList.SelectedItem as PriorityItem;
            if (item == null) return;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag == item.Opportunity) { row.Selected = true; _grid.FirstDisplayedScrollingRowIndex = row.Index; break; }
            }
        }

        private sealed class PriorityItem
        {
            public string Text { get; set; }
            public Opportunity Opportunity { get; set; }
            public override string ToString() { return Text; }
        }
    }

    internal sealed class OpportunityEditor : Form
    {
        private readonly Opportunity _item;
        private readonly Dictionary<string, TextBox> _fields = new Dictionary<string, TextBox>();
        private ComboBox _stage;
        private ComboBox _health;
        private DateTimePicker _nextDate;

        public OpportunityEditor(Opportunity item)
        {
            _item = item; Text = string.IsNullOrWhiteSpace(item.Name) ? "Nueva oportunidad" : "Editar oportunidad"; StartPosition = FormStartPosition.CenterParent; Size = new Size(940, 720); MinimumSize = new Size(800, 620);
            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildTab("Resumen", new[] { Pair("Nombre de oportunidad", "Name"), Pair("Cliente / cuenta", "Customer"), Pair("Industria", "Industry"), Pair("Responsable", "Owner"), Pair("Próximo hito", "NextMilestone"), Pair("Bloqueo actual", "Blocker", true), Pair("Competidores conocidos", "Competitors", true) }, true));
            tabs.TabPages.Add(BuildTab("Diagnóstico", new[] { Pair("Problema operativo", "OperationalProblem", true), Pair("Usuarios afectados", "AffectedUsers", true), Pair("Impacto", "Impact", true), Pair("Interlocutores", "Stakeholders", true), Pair("Proceso de decisión", "DecisionProcess", true), Pair("Presupuesto conocido", "KnownBudget", true), Pair("Plazos", "Timeframe", true), Pair("Criterios de éxito", "SuccessCriteria", true), Pair("Hechos confirmados", "ConfirmedFacts", true), Pair("Supuestos", "Assumptions", true), Pair("Información faltante", "MissingInformation", true) }, false));
            tabs.TabPages.Add(BuildTab("Solución", new[] { Pair("Solución / propuesta de valor", "Solution", true), Pair("Especialidades a coordinar", "Specialties", true), Pair("Arquitectura / HLD-LLD", "Architecture", true), Pair("Alcance", "Scope", true), Pair("Interfaces", "Interfaces", true), Pair("Exclusiones", "Exclusions", true), Pair("Riesgos", "Risks", true), Pair("Mitigaciones", "Mitigations", true), Pair("Insumos de costo / pricing", "CostInputs", true), Pair("Plan de entrega", "DeliveryPlan", true) }, false));
            tabs.TabPages.Add(BuildTab("Valores y cierre", new[] { Pair("Valor pipeline", "PipelineValue"), Pair("Valor de propuesta", "ProposalValue"), Pair("Booking", "BookingValue"), Pair("Contrato adjudicado", "ContractValue"), Pair("Ingreso", "RevenueValue"), Pair("Probabilidad", "Probability"), Pair("Evidencia de probabilidad", "ProbabilityEvidence", true), Pair("Notas de handover", "HandoverNotes", true), Pair("Lecciones reutilizables", "LessonsLearned", true) }, false));
            Controls.Add(tabs);
            FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8), BackColor = Color.FromArgb(244, 247, 250) };
            Button save = new Button { Text = "Guardar oportunidad", Width = 150, Height = 30, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(36, 91, 135), ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; save.Click += delegate { Save(); };
            footer.Controls.Add(save); footer.Controls.Add(new Button { Text = "Cancelar", Width = 88, Height = 30, DialogResult = DialogResult.Cancel }); Controls.Add(footer); AcceptButton = save;
        }

        private TabPage BuildTab(string title, Field[] fields, bool summary)
        {
            TabPage page = new TabPage(title) { Padding = new Padding(12), AutoScroll = true, BackColor = Color.White };
            TableLayoutPanel form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(3) };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200)); form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            if (summary)
            {
                AddCombo(form, "Etapa", new[] { "Identificación", "Calificación", "Diseño de solución", "Propuesta", "Negociación", "Adjudicada", "Perdida", "En pausa" }, _item.Stage, delegate(ComboBox c) { _stage = c; });
                AddCombo(form, "Salud", new[] { "Sin evaluar", "En curso", "En riesgo", "Bloqueada", "Avanza" }, _item.Health, delegate(ComboBox c) { _health = c; });
                form.RowCount++; form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); form.Controls.Add(new Label { Text = "Fecha próximo hito", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI Semibold", 9F) }, 0, form.RowCount - 1);
                _nextDate = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = _item.NextMilestoneDate.HasValue, Value = _item.NextMilestoneDate ?? DateTime.Today, Width = 150, Anchor = AnchorStyles.Left };
                form.Controls.Add(_nextDate, 1, form.RowCount - 1);
            }
            foreach (Field field in fields) AddField(form, field);
            page.Controls.Add(form); return page;
        }

        private void AddCombo(TableLayoutPanel form, string label, string[] options, string selected, Action<ComboBox> assign)
        {
            form.RowCount++; form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); int row = form.RowCount - 1;
            form.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI Semibold", 9F) }, 0, row);
            ComboBox box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Anchor = AnchorStyles.Left }; box.Items.AddRange(options); box.SelectedItem = options.Contains(selected) ? selected : options[0]; form.Controls.Add(box, 1, row); assign(box);
        }

        private void AddField(TableLayoutPanel form, Field field)
        {
            form.RowCount++; int row = form.RowCount - 1; int height = field.Multiline ? 98 : 34; form.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            form.Controls.Add(new Label { Text = field.Label, AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI Semibold", 9F), MaximumSize = new Size(190, 0) }, 0, row);
            PropertyInfo property = typeof(Opportunity).GetProperty(field.Property);
            TextBox box = new TextBox { Text = (string)property.GetValue(_item, null) ?? string.Empty, Multiline = field.Multiline, ScrollBars = field.Multiline ? ScrollBars.Vertical : ScrollBars.None, Dock = field.Multiline ? DockStyle.Fill : DockStyle.None, Width = field.Multiline ? 0 : 600, Anchor = field.Multiline ? AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right : AnchorStyles.Left | AnchorStyles.Right };
            form.Controls.Add(box, 1, row); _fields[field.Property] = box;
        }

        private void Save()
        {
            foreach (KeyValuePair<string, TextBox> pair in _fields) typeof(Opportunity).GetProperty(pair.Key).SetValue(_item, pair.Value.Text.Trim(), null);
            _item.Stage = _stage.SelectedItem.ToString(); _item.Health = _health.SelectedItem.ToString(); _item.NextMilestoneDate = _nextDate.Checked ? (DateTime?)_nextDate.Value.Date : null; _item.UpdatedUtc = DateTime.UtcNow; _item.Repair();
        }

        private static Field Pair(string label, string property) { return new Field { Label = label, Property = property }; }
        private static Field Pair(string label, string property, bool multiline) { return new Field { Label = label, Property = property, Multiline = multiline }; }
        private sealed class Field { public string Label; public string Property; public bool Multiline; }
    }

    internal sealed class WarRoomEditor : Form
    {
        private readonly WarRoomSession _item;
        private readonly Dictionary<string, TextBox> _fields = new Dictionary<string, TextBox>();
        public WarRoomEditor(WarRoomSession item, string opportunityName)
        {
            _item = item; Text = "WarRoom · " + opportunityName; StartPosition = FormStartPosition.CenterParent; Size = new Size(780, 670); MinimumSize = new Size(650, 550);
            Panel form = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(18), BackColor = Color.White };
            string[,] fields = { { "Objetivo del cliente", "Objective" }, { "Fecha límite", "Deadline" }, { "Hechos confirmados", "ConfirmedFacts" }, { "Brechas", "Gaps" }, { "Decisiones pendientes", "PendingDecisions" }, { "Acción siguiente", "NextAction" }, { "Responsable", "Responsible" }, { "Fecha de acción", "DueDate" }, { "Apoyo requerido", "SupportRequired" }, { "Vía de escalamiento", "EscalationPath" }, { "Notas ejecutivas", "ExecutiveSummary" } };
            int top = 12; for (int i = 0; i < fields.GetLength(0); i++) { string label = fields[i, 0]; string property = fields[i, 1]; Label caption = new Label { Text = label, Location = new Point(8, top), Width = 190, Font = new Font("Segoe UI Semibold", 9F) }; TextBox input = new TextBox { Text = (string)typeof(WarRoomSession).GetProperty(property).GetValue(_item, null) ?? string.Empty, Location = new Point(204, top - 3), Width = 520, Multiline = i != 1 && i != 6 && i != 7, Height = i != 1 && i != 6 && i != 7 ? 55 : 25, ScrollBars = ScrollBars.Vertical, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right }; form.Controls.Add(caption); form.Controls.Add(input); _fields[property] = input; top += input.Height + 12; }
            Controls.Add(form); FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) }; Button save = new Button { Text = "Guardar y resumir", Width = 140, DialogResult = DialogResult.OK }; save.Click += delegate { foreach (KeyValuePair<string, TextBox> pair in _fields) typeof(WarRoomSession).GetProperty(pair.Key).SetValue(_item, pair.Value.Text.Trim(), null); }; footer.Controls.Add(save); footer.Controls.Add(new Button { Text = "Cancelar", Width = 82, DialogResult = DialogResult.Cancel }); Controls.Add(footer); AcceptButton = save;
        }
    }
}
