using System.Net.Http.Json;
using System.Drawing;
using ResortManagementSystem.Domain.Entities;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class FeedbackControl : UserControl
    {
        private readonly HttpClient _http;
        private readonly int _companyId;
        private readonly bool _canResolve; // true = Manager (resolve view), false = Staff (log view)

        private DataGridView dgvFeedback = null!;

        private Pager _pager = null!;
        private ComboBox cboCustomer = null!, cboFilter = null!;
        private TextBox txtComments = null!, txtSearch = null!;
        private RadioButton rbFeedback = null!, rbComplaint = null!;
        private Label lblInfo = null!, lblFormTitle = null!, lblFormError = null!;
        private Panel card = null!;
        private Button btnSave = null!, btnCancel = null!;
        private FormValidator _v = null!;
        private Label[] _stats = new Label[4];

        // Manager detail panel
        private Label lblDetailHeader = null!;
        private TextBox txtDetail = null!;
        private Button btnResolve = null!;
        private int? _selectedFeedbackId = null;

        private Dictionary<int, string> _customerNames = new();
        private List<FeedbackRow> _all = new();

        private class LookupItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public override string ToString() => Name;
        }

        private class FeedbackRow
        {
            public int FeedbackId { get; set; }
            public string CustomerName { get; set; } = "";
            public string Comments { get; set; } = "";
            public bool IsComplaint { get; set; }
            public string Type { get; set; } = "Feedback";
            public string Status { get; set; } = "";
            public DateTime CreatedAt { get; set; }
        }

        public FeedbackControl(HttpClient http, int companyId, bool canResolve)
        {
            _http = http;
            _companyId = companyId;
            _canResolve = canResolve;
            Dock = DockStyle.Fill;
            BackColor = ContentBg;
            Padding = new Padding(36, 28, 36, 28);
            BuildUi();
        }

        public async Task LoadAsync()
        {
            await LoadCustomersAsync();
            await LoadFeedbackAsync();
        }

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = ContentBg };

            var topBar = BuildTopBar(
                _canResolve ? "Resolve Complaints" : "Guest Feedback",
                _canResolve ? "Review and resolve escalated guest complaints" : "Log feedback from guests during their stay",
                out _);

            var statsRow = _canResolve
                ? MakeStatsRow(
                    ("Open Complaints", Danger, "need a response"),
                    ("Resolved", Accent, "closed complaints"),
                    ("Oldest Open", AccentAmber, "waiting the longest"),
                    ("Resolution Rate", Accent, "complaints resolved"))
                : MakeStatsRow(
                    ("Total Records", Accent, "feedback and complaints"),
                    ("Open Complaints", Danger, "waiting for a manager"),
                    ("Resolved", Accent, "closed complaints"),
                    ("General Feedback", ChartBlue, "comments and praise"));
            for (int i = 0; i < 4; i++) _stats[i] = (Label)statsRow.Controls[i].Tag;

            // ---- Toolbar ----
            var toolbar = MakeToolbar("+ Log Feedback", out var btnNew, out var btnRefresh);
            txtSearch = MakeSearchBox(56, 240);
            cboFilter = _canResolve
                ? MakeFilterCombo(378, 160, "Open complaints", "Resolved", "All complaints")
                : MakeFilterCombo(378, 160, "All records", "Complaints only", "General feedback", "Open", "Resolved");
            toolbar.Controls.AddRange(new Control[]
            {
                MakeToolbarLabel("Search", 0), txtSearch,
                MakeToolbarLabel("Show", 322), cboFilter
            });
            if (_canResolve) btnNew.Visible = false;   // managers resolve, staff log

            // ---- Staff: log feedback form ----
            card = MakeFormCard(276, out lblFormTitle);
            lblFormTitle.Text = "Log Feedback";
            if (!_canResolve)
            {
                int col1 = 24;
                var lblCustomer = MakeFieldLabel("Customer", col1, 58);
                cboCustomer = MakeComboBox(col1 + LabelWidth, 58);
                var lblComments = MakeFieldLabel("Comments", col1, 96);
                txtComments = new TextBox
                {
                    Left = col1 + LabelWidth,
                    Top = 93,
                    Width = 620,
                    Height = 66,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    BorderStyle = BorderStyle.FixedSingle,
                    PlaceholderText = "What did the guest say? (at least 5 characters)"
                };
                var lblType = MakeFieldLabel("Type", col1, 176);
                rbFeedback = new RadioButton { Text = "General feedback", Left = col1 + LabelWidth, Top = 172, AutoSize = true, Checked = true };
                rbComplaint = new RadioButton { Text = "Complaint (needs Manager follow-up)", Left = col1 + LabelWidth + 170, Top = 172, AutoSize = true };

                lblFormError = new Label { Left = col1, Top = 204, Width = 900, Height = 22, ForeColor = Danger, Font = new Font("Segoe UI", 9f, FontStyle.Bold), UseMnemonic = false };

                btnSave = MakePillButton("Log Feedback", Accent, Color.White, col1, 232, 160);
                btnCancel = MakePillButton("Cancel", CardBg, TextMuted, col1 + 172, 232, 110);

                card.Controls.AddRange(new Control[]
                {
                    lblCustomer, cboCustomer, lblComments, txtComments, lblType, rbFeedback, rbComplaint,
                    lblFormError, btnSave, btnCancel
                });

                _v = new FormValidator(lblFormError);
                _v.Watch(cboCustomer, () => cboCustomer.SelectedItem is null ? "Select a customer." : null);
                _v.Watch(txtComments, () => InputRules.Text(txtComments.Text, "Comments", 5, 500, required: true));

                btnNew.Click += (s, e) => OpenForm();
                btnSave.Click += async (s, e) => await AddFeedbackAsync();
                btnCancel.Click += (s, e) => CloseForm();
            }

            var spacerMid = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = ContentBg };
            var spacerBottom = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = ContentBg };

            // Dock = Top: the LAST control added ends up on top
            header.Controls.Add(spacerBottom);
            header.Controls.Add(card);
            header.Controls.Add(toolbar);
            header.Controls.Add(spacerMid);
            header.Controls.Add(statsRow);
            header.Controls.Add(topBar);

            lblInfo = new Label { Dock = DockStyle.Top, Height = 24, Text = "Ready.", ForeColor = TextMuted, Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), Padding = new Padding(0, 4, 0, 0) };

            // ---- Grid ----
            dgvFeedback = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvFeedback);
            AddTextColumn(dgvFeedback, nameof(FeedbackRow.FeedbackId), "ID", 40);
            AddTextColumn(dgvFeedback, nameof(FeedbackRow.CustomerName), "Customer", 120);
            AddTextColumn(dgvFeedback, nameof(FeedbackRow.Comments), "Comments", 300);
            AddTextColumn(dgvFeedback, nameof(FeedbackRow.Type), "Type", 90);
            AddTextColumn(dgvFeedback, nameof(FeedbackRow.Status), "Status", 90);
            AddTextColumn(dgvFeedback, nameof(FeedbackRow.CreatedAt), "Logged", 130);
            dgvFeedback.Columns[nameof(FeedbackRow.CreatedAt)].DefaultCellStyle.Format = "MM/dd/yyyy h:mm tt";
            MakeBadgeColumn(dgvFeedback, nameof(FeedbackRow.Type), t => t == "Complaint" ? BadgeRed : BadgeGray);
            MakeBadgeColumn(dgvFeedback, nameof(FeedbackRow.Status), status => status == "Resolved" ? BadgeGreen : BadgeAmber);

            btnRefresh.Click += async (s, e) => await LoadAsync();
            txtSearch.TextChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };
            cboFilter.SelectedIndexChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };

            if (_canResolve)
            {
                // Detail panel under the grid: full comment + resolve button
                var spacerGrid = new Panel { Dock = DockStyle.Bottom, Height = 12, BackColor = ContentBg };
                var detailPanel = new Panel { Dock = DockStyle.Bottom, Height = 150, BackColor = CardBg, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(20, 14, 20, 14) };

                txtDetail = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    BorderStyle = BorderStyle.None,
                    BackColor = CardBg,
                    Font = new Font("Segoe UI", 10f),
                    ScrollBars = ScrollBars.Vertical
                };
                lblDetailHeader = new Label { Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextDark, UseMnemonic = false, Text = "Select a complaint to see the full comment." };
                var detailRight = new Panel { Dock = DockStyle.Right, Width = 190, BackColor = CardBg };
                btnResolve = MakePillButton("Mark as Resolved", Accent, Color.White, 10, 6, 170);
                detailRight.Controls.Add(btnResolve);

                detailPanel.Controls.Add(txtDetail);        // Fill first
                detailPanel.Controls.Add(lblDetailHeader);
                detailPanel.Controls.Add(detailRight);

                btnResolve.Click += async (s, e) => await ResolveFeedbackAsync();
                dgvFeedback.SelectionChanged += (s, e) => ShowDetail();

                Controls.Add(dgvFeedback);
                Controls.Add(spacerGrid);
                Controls.Add(detailPanel);
            }
            else
            {
                Controls.Add(dgvFeedback);
            }

            _pager = new Pager(dgvFeedback);
            Controls.Add(_pager);
            Controls.Add(lblInfo);
            Controls.Add(header);
        }

        // ------------------------------------------------------------ Data

        private async Task LoadCustomersAsync()
        {
            try
            {
                var customers = await _http.GetFromJsonAsync<List<Customer>>($"/tenant/{_companyId}/customers?includeArchived=true") ?? new();
                _customerNames = customers.ToDictionary(c => c.CustomerId, c => c.FullName);

                if (!_canResolve)
                {
                    cboCustomer.Items.Clear();
                    foreach (var c in customers.Where(c => !c.IsArchived))
                        cboCustomer.Items.Add(new LookupItem { Id = c.CustomerId, Name = $"#{c.CustomerId} — {c.FullName}" });
                    if (cboCustomer.Items.Count > 0) cboCustomer.SelectedIndex = 0;
                }
            }
            catch (Exception ex) { MessageBox.Show($"Failed to load customers: {ex.Message}"); }
        }

        private async Task LoadFeedbackAsync()
        {
            try
            {
                lblInfo.Text = "Loading feedback…";
                var feedback = await _http.GetFromJsonAsync<List<Feedback>>($"/tenant/{_companyId}/feedback") ?? new();
                if (_canResolve) feedback = feedback.Where(f => f.IsComplaint).ToList();

                _all = feedback.Select(f => new FeedbackRow
                {
                    FeedbackId = f.FeedbackId,
                    CustomerName = _customerNames.TryGetValue(f.CustomerId, out var name) ? name : $"Customer #{f.CustomerId}",
                    Comments = f.Comments ?? "",
                    IsComplaint = f.IsComplaint,
                    Type = f.IsComplaint ? "Complaint" : "Feedback",
                    Status = f.Status ?? "",
                    CreatedAt = f.CreatedAt
                }).ToList();

                UpdateStats();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                lblInfo.Text = "Failed to load feedback.";
                MessageBox.Show($"Failed to load feedback: {ex.Message}");
            }
        }

        private void UpdateStats()
        {
            var complaints = _all.Where(r => r.IsComplaint).ToList();
            var open = complaints.Where(r => r.Status != "Resolved").ToList();
            var resolved = complaints.Count - open.Count;

            if (_canResolve)
            {
                _stats[0].Text = open.Count.ToString();
                _stats[1].Text = resolved.ToString();
                _stats[2].Text = open.Count == 0
                    ? "None"
                    : $"{Math.Max(0, (DateTime.UtcNow - open.Min(r => r.CreatedAt)).Days)} day(s)";
                _stats[3].Text = complaints.Count == 0 ? "-" : $"{resolved * 100 / complaints.Count}%";
            }
            else
            {
                _stats[0].Text = _all.Count.ToString();
                _stats[1].Text = open.Count.ToString();
                _stats[2].Text = resolved.ToString();
                _stats[3].Text = _all.Count(r => !r.IsComplaint).ToString();
            }
        }

        private void ApplyFilter()
        {
            var term = txtSearch.Text.Trim();
            var filter = cboFilter.SelectedItem?.ToString() ?? "";
            IEnumerable<FeedbackRow> q = _all;

            q = filter switch
            {
                "Open complaints" or "Open" => q.Where(r => r.Status != "Resolved"),
                "Resolved" => q.Where(r => r.Status == "Resolved"),
                "Complaints only" => q.Where(r => r.IsComplaint),
                "General feedback" => q.Where(r => !r.IsComplaint),
                _ => q
            };

            if (term.Length > 0)
                q = q.Where(r => r.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase)
                              || r.Comments.Contains(term, StringComparison.OrdinalIgnoreCase));

            var rows = q.ToList();
            _pager.Bind(rows);
            lblInfo.Text = $"Showing {rows.Count} of {_all.Count} record(s).";
            if (_canResolve) ShowDetail();
        }

        // ------------------------------------------------------------ Staff: log feedback

        private void OpenForm()
        {
            if (cboCustomer.Items.Count > 0) cboCustomer.SelectedIndex = 0;
            txtComments.Clear();
            rbFeedback.Checked = true;
            _v.Reset();
            card.Visible = true;
            txtComments.Focus();
        }

        private void CloseForm()
        {
            txtComments.Clear();
            _v.Reset();
            card.Visible = false;
        }

        private async Task AddFeedbackAsync()
        {
            if (!_v.ValidateAll()) return;

            var customer = (LookupItem)cboCustomer.SelectedItem!;
            var feedback = new Feedback
            {
                CustomerId = customer.Id,
                Comments = txtComments.Text.Trim(),
                IsComplaint = rbComplaint.Checked
            };

            var response = await _http.PostAsJsonAsync($"/tenant/{_companyId}/feedback", feedback);
            if (response.IsSuccessStatusCode)
            {
                lblInfo.Text = "Feedback logged.";
                CloseForm();
                await LoadFeedbackAsync();
            }
            else
            {
                lblInfo.Text = "Failed to log feedback.";
                MessageBox.Show($"Failed to log feedback: {response.StatusCode}\n{await response.Content.ReadAsStringAsync()}");
            }
        }

        // ------------------------------------------------------------ Manager: resolve

        private void ShowDetail()
        {
            if (dgvFeedback.CurrentRow?.DataBoundItem is FeedbackRow r)
            {
                _selectedFeedbackId = r.FeedbackId;
                lblDetailHeader.Text = $"{r.CustomerName}   ·   {r.CreatedAt:MMM d, yyyy h:mm tt}   ·   {r.Status}";
                txtDetail.Text = r.Comments;
                btnResolve.Enabled = r.Status != "Resolved";
            }
            else
            {
                _selectedFeedbackId = null;
                lblDetailHeader.Text = "Select a complaint to see the full comment.";
                txtDetail.Clear();
                btnResolve.Enabled = false;
            }
        }

        private async Task ResolveFeedbackAsync()
        {
            if (_selectedFeedbackId is null) { MessageBox.Show("Select a complaint from the list first."); return; }

            var response = await _http.PutAsync($"/tenant/{_companyId}/feedback/{_selectedFeedbackId}/resolve", null);
            if (response.IsSuccessStatusCode)
            {
                lblInfo.Text = "Marked as resolved.";
                await LoadFeedbackAsync();
            }
            else
            {
                lblInfo.Text = "Failed to resolve.";
                MessageBox.Show($"Failed to resolve: {response.StatusCode}");
            }
        }
    }
}