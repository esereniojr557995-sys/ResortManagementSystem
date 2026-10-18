using System.Drawing;
using System.Globalization;
using System.Net.Http.Json;
using ResortManagementSystem.Domain.Entities;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class CompaniesControl : UserControl
    {
        private readonly HttpClient _http;

        private DataGridView dgvCompanies = null!;
        private TextBox txtCompanyCode = null!, txtCompanyName = null!;
        private TextBox txtAdminEmail = null!, txtAdminPassword = null!, txtSearch = null!;
        private PlanSelector planSelector = null!;
        private NumericUpDown nudMonths = null!;
        private DateTimePicker dtpStart = null!;
        private Label lblSelectedCompany = null!, lblCurrent = null!, lblSummary = null!, lblEndDate = null!;
        private Label lblStatus = null!, lblNewPlanHint = null!;
        private Button btnProvision = null!, btnNewSub = null!, btnToggleActive = null!, btnHistory = null!;

        private List<CompanyOverviewDto> _companies = new();
        private CompanyOverviewDto? _selected;

        // What the grid shows (already formatted as text)
        private class CompanyRow
        {
            public int Id { get; set; }
            public string Code { get; set; } = "";
            public string Name { get; set; } = "";
            public string Tier { get; set; } = "";
            public string PlanEnds { get; set; } = "";
            public string Remaining { get; set; } = "";
            public int Months { get; set; }
            public string Total { get; set; } = "";
            public string Agreement { get; set; } = "";
            public string AcceptedBy { get; set; } = "";
            public string AcceptedAt { get; set; } = "";
            public string Status { get; set; } = "";
        }

        public CompaniesControl(HttpClient http)
        {
            _http = http;
            Dock = DockStyle.Fill;
            BackColor = ContentBg;
            Padding = new Padding(36, 28, 36, 28);

            // On short windows the page scrolls instead of squeezing the grid to nothing
            AutoScroll = true;
            AutoScrollMinSize = new Size(1100, 1000);

            BuildUi();
        }

        public async Task LoadAsync() => await LoadCompaniesAsync();

        // ------------------------------------------------------------------ UI

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = ContentBg };

            var topBar = BuildTopBar("Companies", "Provision tenant accounts, manage subscriptions and publish the terms (Super Admin)", out _);

            // ---------- Card 1: new company ----------
            var formCard = MakeCard(0, 176);
            formCard.Dock = DockStyle.Top;
            formCard.Padding = new Padding(24, 16, 24, 10);

            var cardTitle = new Label
            {
                Text = "New Company",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = TextDark,
                Dock = DockStyle.Top,
                Height = 32,
                UseMnemonic = false
            };

            // Labels size themselves (no more "Company" / "Admin" cut-offs)
            Label FieldLabel(string text, int leftMargin) => new()
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = TextMuted,
                Margin = new Padding(leftMargin, 0, 14, 0),
                UseMnemonic = false
            };
            TextBox Field() => new()
            {
                Width = 300,
                Anchor = AnchorStyles.Left,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 6, 0, 6)
            };

            txtCompanyCode = Field();
            txtCompanyName = Field();
            txtAdminEmail = Field();
            txtAdminPassword = Field();
            txtAdminPassword.UseSystemPasswordChar = true;

            var form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 3, BackColor = CardBg };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            form.Controls.Add(FieldLabel("Company Code", 0), 0, 0);
            form.Controls.Add(txtCompanyCode, 1, 0);
            form.Controls.Add(FieldLabel("Company Name", 40), 2, 0);
            form.Controls.Add(txtCompanyName, 3, 0);

            form.Controls.Add(FieldLabel("Admin Email", 0), 0, 1);
            form.Controls.Add(txtAdminEmail, 1, 1);
            form.Controls.Add(FieldLabel("Admin Password", 40), 2, 1);
            form.Controls.Add(txtAdminPassword, 3, 1);

            // Provision sits right beside the fields it submits
            btnProvision = MakePillButton("Provision Company", Accent, Color.White, 0, 0, 190);
            btnProvision.Margin = new Padding(40, 0, 0, 0);
            btnProvision.Anchor = AnchorStyles.Left;
            form.Controls.Add(btnProvision, 4, 0);
            form.SetRowSpan(btnProvision, 2);

            lblNewPlanHint = new Label
            {
                AutoSize = true,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Margin = new Padding(0, 6, 0, 0),
                UseMnemonic = false
            };
            form.Controls.Add(lblNewPlanHint, 0, 2);
            form.SetColumnSpan(lblNewPlanHint, 5);

            formCard.Controls.Add(form);        // Fill first
            formCard.Controls.Add(cardTitle);

            // ---------- Card 2: plan picker (left) + subscription actions (right) ----------
            var planCard = MakeCard(0, 376);
            planCard.Dock = DockStyle.Top;
            planCard.Padding = new Padding(24, 16, 24, 14);

            var planTitle = new Label
            {
                Text = "Choose the plan that's right for the resort",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = TextDark,
                Dock = DockStyle.Top,
                Height = 34,
                UseMnemonic = false
            };

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = CardBg };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 470));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            planSelector = new PlanSelector { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 24, 0), MinimumSize = new Size(560, 290) };

            var right = new Panel { Dock = DockStyle.Fill, BackColor = CardBg, Margin = new Padding(0) };

            lblSelectedCompany = new Label { Text = "For: (select a company below)", Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = TextDark, Left = 0, Top = 0, Width = 440, Height = 24, AutoEllipsis = true, UseMnemonic = false };
            lblCurrent = new Label { Text = "", Font = new Font("Segoe UI", 8.5f), ForeColor = TextMuted, Left = 0, Top = 26, Width = 440, Height = 20, AutoEllipsis = true, UseMnemonic = false };

            var lblDuration = MakeFieldLabel("Duration", 0, 62);
            nudMonths = new NumericUpDown { Left = 115, Top = 58, Width = 90, Minimum = 1, Maximum = 36, Value = 1 };
            var lblMonths = new Label { Text = "month(s)", Left = 214, Top = 62, AutoSize = true, ForeColor = TextMuted };

            var lblStart = MakeFieldLabel("Start date", 0, 100);
            dtpStart = new DateTimePicker { Left = 115, Top = 96, Width = 170, Format = DateTimePickerFormat.Short, Value = DateTime.Today };

            lblSummary = new Label { Left = 0, Top = 134, Width = 440, Height = 30, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = AccentDark, UseMnemonic = false };
            lblEndDate = new Label { Left = 0, Top = 164, Width = 440, Height = 20, Font = new Font("Segoe UI", 8.5f), ForeColor = TextMuted, UseMnemonic = false };

            btnNewSub = MakePillButton("New Subscription", Accent, Color.White, 0, 198, 200);
            btnToggleActive = MakePillButton("Suspend", CardBg, Danger, 212, 198, 110);
            btnHistory = MakePillButton("History", CardBg, TextDark, 334, 198, 100);

            var hint = new Label
            {
                Text = "After a new subscription, the company's Admin must accept the Terms and Conditions the next time they sign in.",
                Left = 0,
                Top = 244,
                Width = 440,
                Height = 40,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                UseMnemonic = false
            };

            right.Controls.AddRange(new Control[]
            {
                lblSelectedCompany, lblCurrent, lblDuration, nudMonths, lblMonths, lblStart, dtpStart,
                lblSummary, lblEndDate, btnNewSub, btnToggleActive, btnHistory, hint
            });

            body.Controls.Add(planSelector, 0, 0);
            body.Controls.Add(right, 1, 0);

            planCard.Controls.Add(body);        // Fill first
            planCard.Controls.Add(planTitle);

            var spacer1 = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = ContentBg };
            var spacer2 = new Panel { Dock = DockStyle.Top, Height = 20, BackColor = ContentBg };
            var spacer3 = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = ContentBg };

            // Dock.Top controls stack in reverse add order: last added = top-most
            header.Controls.Add(spacer3);
            header.Controls.Add(planCard);
            header.Controls.Add(spacer1);
            header.Controls.Add(formCard);
            header.Controls.Add(spacer2);
            header.Controls.Add(topBar);

            // ---------- Toolbar above the grid ----------
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = ContentBg };
            var toolbarRight = new Panel { Dock = DockStyle.Right, Width = 320, BackColor = ContentBg };
            var btnTerms = MakePillButton("Terms and Conditions", TextDark, Color.White, 0, 8, 190);
            var btnLoad = MakePillButton("Refresh", CardBg, TextDark, 200, 8, 110);
            toolbarRight.Controls.Add(btnTerms);
            toolbarRight.Controls.Add(btnLoad);

            var searchLabel = MakeToolbarLabel("Search", 0);
            txtSearch = MakeSearchBox(62, 300);
            txtSearch.PlaceholderText = "Company name or code";

            toolbar.Controls.Add(toolbarRight);
            toolbar.Controls.Add(searchLabel);
            toolbar.Controls.Add(txtSearch);

            lblStatus = new Label { Dock = DockStyle.Top, Height = 24, Text = "Select a company below to manage its subscription.", ForeColor = TextMuted, Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), Padding = new Padding(0, 4, 0, 0), UseMnemonic = false };

            // ---------- Grid ----------
            dgvCompanies = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvCompanies);
            AddTextColumn(dgvCompanies, nameof(CompanyRow.Id), "ID", 40);
            AddTextColumn(dgvCompanies, nameof(CompanyRow.Code), "Code", 95);
            AddTextColumn(dgvCompanies, nameof(CompanyRow.Name), "Company", 230);
            AddTextColumn(dgvCompanies, nameof(CompanyRow.Tier), "Plan", 95);
            AddTextColumn(dgvCompanies, nameof(CompanyRow.PlanEnds), "Plan ends", 125);
            AddTextColumn(dgvCompanies, nameof(CompanyRow.Remaining), "Remaining", 125);
            AddTextColumn(dgvCompanies, nameof(CompanyRow.Months), "Months", 65);
            AddTextColumn(dgvCompanies, nameof(CompanyRow.Total), "Total fee", 100);
            AddTextColumn(dgvCompanies, nameof(CompanyRow.Agreement), "Terms", 100);
            AddTextColumn(dgvCompanies, nameof(CompanyRow.AcceptedBy), "Accepted by", 150);
            AddTextColumn(dgvCompanies, nameof(CompanyRow.AcceptedAt), "Accepted at", 170);
            AddTextColumn(dgvCompanies, nameof(CompanyRow.Status), "Status", 105);

            MakeBadgeColumn(dgvCompanies, nameof(CompanyRow.Tier), tier => tier switch
            {
                "Medium" => BadgeBlue,
                "Small" => BadgeGreen,
                _ => BadgeGray
            });
            MakeBadgeColumn(dgvCompanies, nameof(CompanyRow.Agreement), a => a switch
            {
                "Accepted" => BadgeGreen,
                "Pending" => BadgeAmber,
                _ => BadgeGray
            });
            MakeBadgeColumn(dgvCompanies, nameof(CompanyRow.Remaining), RemainingBadge);
            MakeBadgeColumn(dgvCompanies, nameof(CompanyRow.Status), st => st == "Active" ? BadgeGreen : BadgeRed);

            Controls.Add(dgvCompanies);
            Controls.Add(lblStatus);
            Controls.Add(toolbar);
            Controls.Add(header);

            // Events last, so every control exists when they first fire
            planSelector.SelectedTierChanged += (s, e) =>
            {
                UpdateSummary();
                lblStatus.Text = $"{planSelector.SelectedTier} plan selected.";
            };
            nudMonths.ValueChanged += (s, e) => UpdateSummary();
            dtpStart.ValueChanged += (s, e) => UpdateSummary();
            btnProvision.Click += async (s, e) => await AddCompanyAsync();
            txtAdminPassword.KeyDown += async (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                await AddCompanyAsync();
            };
            btnNewSub.Click += async (s, e) => await NewSubscriptionAsync();
            btnToggleActive.Click += async (s, e) => await ToggleActiveAsync();
            btnHistory.Click += async (s, e) => await ShowHistoryAsync();
            btnTerms.Click += (s, e) => { using var f = new TermsEditorForm(_http); f.ShowDialog(FindForm()); };
            btnLoad.Click += async (s, e) => await LoadCompaniesAsync();
            txtSearch.TextChanged += (s, e) => ApplyFilter();
            dgvCompanies.SelectionChanged += DgvCompanies_SelectionChanged;
            dgvCompanies.CellDoubleClick += async (s, e) => { if (e.RowIndex >= 0) await ShowHistoryAsync(); };

            UpdateSummary();
            UpdateActionState();
        }

        private static decimal PriceOf(PlanOption plan) =>
            decimal.TryParse(plan.Price.Replace("₱", "").Replace(",", "").Trim(),
                NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;

        private void UpdateSummary()
        {
            var plan = planSelector.Plans.First(p => p.Name == planSelector.SelectedTier);
            var months = (int)nudMonths.Value;
            var start = dtpStart.Value.Date;
            var end = start.AddMonths(months);

            lblSummary.Text = $"{plan.Name} plan  x  {months} month{(months == 1 ? "" : "s")}  =  ₱{PriceOf(plan) * months:N0}";
            lblEndDate.Text = $"Runs {start:MMM d, yyyy} to {end:MMM d, yyyy}";
            lblNewPlanHint.Text = $"A newly provisioned company starts on the {plan.Name} plan selected below, for 1 month.";
        }

        // Buttons that need a selected company are disabled until one is chosen
        private void UpdateActionState()
        {
            var has = _selected is not null;
            btnNewSub.Enabled = has;
            btnToggleActive.Enabled = has;
            btnHistory.Enabled = has;
        }

        // ---------------------------------------------------------------- Data

        private static string RemainingText(DateTime? end)
        {
            if (end is null) return "-";
            var days = (end.Value.Date - DateTime.Today).Days;
            if (days < 0) return "Expired";
            if (days == 0) return "Ends today";
            return $"{days} day{(days == 1 ? "" : "s")} left";
        }

        // Red = expired, amber = a week or less, green = plenty of time
        private static (Color Bg, Color Fg) RemainingBadge(string text)
        {
            if (text == "Expired") return BadgeRed;
            if (text == "Ends today") return BadgeAmber;
            if (text == "-") return BadgeGray;
            var days = int.TryParse(text.Split(' ')[0], out var d) ? d : 99;
            return days <= 7 ? BadgeAmber : BadgeGreen;
        }

        private static CompanyRow ToRow(CompanyOverviewDto c) => new()
        {
            Id = c.CompanyId,
            Code = c.CompanyCode,
            Name = c.CompanyName,
            Tier = c.SubscriptionTier,
            PlanEnds = c.EndDate is null ? "-" : c.EndDate.Value.ToString("MMM d, yyyy"),
            Remaining = RemainingText(c.EndDate),
            Months = c.Months,
            Total = $"₱{c.TotalPrice:N0}",
            Agreement = c.Agreement,
            AcceptedBy = c.AcceptedBy ?? "-",
            AcceptedAt = c.AcceptedAt is null ? "-" : SubscriptionFormat.LocalStamp(c.AcceptedAt.Value),
            Status = c.IsSubscriptionActive ? "Active" : "Suspended"
        };

        private async Task LoadCompaniesAsync(int? reselectId = null)
        {
            try
            {
                lblStatus.Text = "Loading companies…";
                var keep = reselectId ?? _selected?.CompanyId;   // captured before binding resets the selection

                _companies = await _http.GetFromJsonAsync<List<CompanyOverviewDto>>("/admin/companies/overview") ?? new();
                ApplyFilter(keep);

                lblStatus.Text = $"Loaded {_companies.Count} company account(s).";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Failed to load companies.";
                MessageBox.Show($"Could not reach the server: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Binds the (optionally searched) list and keeps the same company selected when it can
        private void ApplyFilter(int? keep = null)
        {
            keep ??= _selected?.CompanyId;

            var q = txtSearch.Text.Trim();
            IEnumerable<CompanyOverviewDto> source = _companies;
            if (q.Length > 0)
                source = source.Where(c =>
                    c.CompanyName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    c.CompanyCode.Contains(q, StringComparison.OrdinalIgnoreCase));

            dgvCompanies.DataSource = source.Select(ToRow).ToList();

            if (keep is not null) SelectRow(keep.Value);
            UpdateActionState();
        }

        private void SelectRow(int companyId)
        {
            foreach (DataGridViewRow r in dgvCompanies.Rows)
            {
                if (r.DataBoundItem is CompanyRow row && row.Id == companyId)
                {
                    dgvCompanies.CurrentCell = r.Cells[0];
                    r.Selected = true;
                    return;
                }
            }
        }

        private void DgvCompanies_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvCompanies.CurrentRow?.DataBoundItem is not CompanyRow row)
            {
                _selected = null;
                lblSelectedCompany.Text = "For: (select a company below)";
                lblCurrent.Text = "";
                UpdateActionState();
                return;
            }

            _selected = _companies.FirstOrDefault(c => c.CompanyId == row.Id);
            if (_selected is null) { UpdateActionState(); return; }

            planSelector.SelectedTier = _selected.SubscriptionTier;
            lblSelectedCompany.Text = $"For: {_selected.CompanyName}";
            lblCurrent.Text = $"Current: {_selected.SubscriptionTier} plan, {(_selected.IsSubscriptionActive ? "active" : "suspended")}, ends {SubscriptionFormat.PlanEnds(_selected.EndDate)}";
            btnToggleActive.Text = _selected.IsSubscriptionActive ? "Suspend" : "Reactivate";
            btnToggleActive.ForeColor = _selected.IsSubscriptionActive ? Danger : AccentDark;
            UpdateSummary();
            UpdateActionState();
            lblStatus.Text = $"Selected {_selected.CompanyName}: {_selected.SubscriptionTier} plan, ends {SubscriptionFormat.PlanEnds(_selected.EndDate)}.";
        }

        // ------------------------------------------------------------- Actions

        private async Task AddCompanyAsync()
        {
            if (string.IsNullOrWhiteSpace(txtCompanyCode.Text) || string.IsNullOrWhiteSpace(txtCompanyName.Text)
                || string.IsNullOrWhiteSpace(txtAdminEmail.Text))
            {
                MessageBox.Show("Company Code, Company Name, and Admin Email are required.");
                return;
            }
            if (!txtAdminEmail.Text.Contains('@'))
            {
                MessageBox.Show("Please enter a valid Admin Email address.");
                return;
            }
            if (txtAdminPassword.Text.Length < 8)
            {
                MessageBox.Show("Admin password must be at least 8 characters.");
                return;
            }

            var body = new
            {
                companyCode = txtCompanyCode.Text.Trim(),
                companyName = txtCompanyName.Text.Trim(),
                subscriptionTier = planSelector.SelectedTier,
                adminEmail = txtAdminEmail.Text.Trim(),
                adminPassword = txtAdminPassword.Text
            };

            btnProvision.Enabled = false;
            try
            {
                var response = await _http.PostAsJsonAsync("/companies", body);
                if (response.IsSuccessStatusCode)
                {
                    // Select the new company in the grid after reloading
                    var created = await response.Content.ReadFromJsonAsync<CompanyOverviewDto>();

                    txtCompanyCode.Clear();
                    txtCompanyName.Clear();
                    txtAdminEmail.Clear();
                    txtAdminPassword.Clear();
                    txtSearch.Clear();
                    await LoadCompaniesAsync(created?.CompanyId);
                    lblStatus.Text = "Company provisioned with a 1-month subscription and its first Admin login. Remember to register its database via company-databases before it can be used.";
                }
                else
                {
                    lblStatus.Text = "Failed to provision company.";
                    MessageBox.Show($"Failed to add company:\n{await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Failed to provision company.";
                MessageBox.Show($"Could not reach the server: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnProvision.Enabled = true;
            }
        }

        private async Task NewSubscriptionAsync()
        {
            if (_selected is null) { MessageBox.Show("Select a company from the list first."); return; }

            var companyId = _selected.CompanyId;
            var companyName = _selected.CompanyName;
            var tier = planSelector.SelectedTier;
            var months = (int)nudMonths.Value;
            var price = PriceOf(planSelector.Plans.First(p => p.Name == tier));

            var headline = tier == _selected.SubscriptionTier
                ? $"Renew {companyName} on the {tier} plan?"
                : $"Move {companyName} from {_selected.SubscriptionTier} to {tier}?";

            var confirm = MessageBox.Show(
                $"{headline}\n\nPlan: {tier}\nDuration: {months} month(s), starting {dtpStart.Value:MMM d, yyyy}\nTotal fee: ₱{price * months:N0}\n\nThe current subscription will be replaced, and the company's Admin must accept the Terms and Conditions at the next sign-in.",
                "Confirm new subscription", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            btnNewSub.Enabled = false;
            try
            {
                var response = await _http.PostAsJsonAsync($"/companies/{companyId}/subscriptions",
                    new { tier, months, startDate = dtpStart.Value.Date });

                if (response.IsSuccessStatusCode)
                {
                    await LoadCompaniesAsync(companyId);
                    lblStatus.Text = $"{tier} subscription ({months} month(s)) created for {companyName}. Its Admin will be asked to accept the terms at next sign-in.";
                }
                else
                {
                    lblStatus.Text = "Failed to create the subscription.";
                    MessageBox.Show($"Failed to create the subscription:\n{await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Failed to create the subscription.";
                MessageBox.Show($"Could not reach the server: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UpdateActionState();
            }
        }

        private async Task ToggleActiveAsync()
        {
            if (_selected is null) { MessageBox.Show("Select a company from the list first."); return; }

            var companyId = _selected.CompanyId;
            var makeActive = !_selected.IsSubscriptionActive;

            if (!makeActive)
            {
                var confirm = MessageBox.Show(
                    $"Suspend {_selected.CompanyName}?\n\nNobody from this company will be able to sign in until it is reactivated.",
                    "Suspend company", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;
            }

            try
            {
                var response = await _http.PutAsJsonAsync($"/companies/{companyId}/active", new { isActive = makeActive });
                if (response.IsSuccessStatusCode)
                {
                    await LoadCompaniesAsync(companyId);
                    lblStatus.Text = makeActive ? "Company reactivated." : "Company suspended.";
                }
                else
                {
                    MessageBox.Show($"Failed to update the company: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not reach the server: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ShowHistoryAsync()
        {
            if (_selected is null) { MessageBox.Show("Select a company from the list first."); return; }

            try
            {
                var list = await _http.GetFromJsonAsync<List<Subscription>>($"/companies/{_selected.CompanyId}/subscriptions") ?? new();

                var rows = list.Select(s => new
                {
                    Plan = s.Tier,
                    Period = $"{s.StartDate:MMM d, yyyy} to {s.EndDate:MMM d, yyyy}",
                    Months = s.Months,
                    Total = $"₱{s.TotalPrice:N0}",
                    Status = s.Status,
                    Terms = s.TermsVersion is null ? "Not accepted" : $"Version {s.TermsVersion}",
                    AcceptedBy = s.AcceptedBy ?? "-",
                    AcceptedAt = s.AcceptedAt is null ? "-" : SubscriptionFormat.LocalStamp(s.AcceptedAt.Value)
                }).ToList();

                var grid = new DataGridView { Dock = DockStyle.Fill };
                StyleGrid(grid);
                grid.DefaultCellStyle.SelectionForeColor = Color.White;
                AddTextColumn(grid, "Plan", "Plan", 70);
                AddTextColumn(grid, "Period", "Period", 200);
                AddTextColumn(grid, "Months", "Months", 60);
                AddTextColumn(grid, "Total", "Total", 80);
                AddTextColumn(grid, "Status", "Status", 80);
                AddTextColumn(grid, "Terms", "Terms", 90);
                AddTextColumn(grid, "AcceptedBy", "Accepted by", 140);
                AddTextColumn(grid, "AcceptedAt", "Accepted at", 150);
                grid.DataSource = rows;

                using var popup = new DrilldownForm($"Subscription history: {_selected.CompanyName} ({rows.Count})", grid);
                popup.Width = 1100;
                popup.ShowDialog(FindForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not reach the server: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}