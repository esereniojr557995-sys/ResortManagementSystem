using System.Globalization;
using System.Net.Http.Json;
using System.Drawing;
using ResortManagementSystem.Domain.Entities;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class PromotionsControl : UserControl
    {
        private readonly HttpClient _http;
        private readonly int _companyId;

        private DataGridView dgvPromotions = null!;

        private Pager _pager = null!;
        private TextBox txtCode = null!, txtDescription = null!, txtDiscount = null!, txtSearch = null!;
        private ComboBox cboEligibleCategory = null!, cboEligibleFilter = null!;
        private CheckBox chkActive = null!, chkShowArchived = null!;
        private Label lblInfo = null!, lblFormTitle = null!, lblFormError = null!;
        private Panel card = null!;
        private Button btnSave = null!, btnCancel = null!, btnArchive = null!, btnEmail = null!, btnEmailLog = null!;
        private Label statTotal = null!, statActive = null!, statInactive = null!, statHighest = null!;
        private FormValidator _v = null!;

        private int? _selectedId = null;
        private List<PromotionRow> _all = new();

        // Blank "Eligible For" is shown as "All"
        private class PromotionRow
        {
            public int PromotionId { get; set; }
            public string Code { get; set; } = "";
            public string Description { get; set; } = "";
            public decimal DiscountPercent { get; set; }
            public string EligibleCategory { get; set; } = "All";
            public bool IsActive { get; set; }
            public bool IsArchived { get; set; }
        }

        public PromotionsControl(HttpClient http, int companyId)
        {
            _http = http;
            _companyId = companyId;
            Dock = DockStyle.Fill;
            BackColor = ContentBg;
            Padding = new Padding(36, 28, 36, 28);
            BuildUi();
        }

        public async Task LoadAsync() => await LoadPromotionsAsync();

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = ContentBg };
            var topBar = BuildTopBar("Promotions & Loyalty Rules", "Discount tiers and promo codes applied at checkout", out _);

            var statsRow = MakeStatsRow(
                ("Total Promotions", Accent, "not archived"),
                ("Active", Accent, "available at booking"),
                ("Inactive", AccentAmber, "switched off"),
                ("Highest Discount", ChartBlue, "best offer right now")
            );
            statTotal = (Label)statsRow.Controls[0].Tag;
            statActive = (Label)statsRow.Controls[1].Tag;
            statInactive = (Label)statsRow.Controls[2].Tag;
            statHighest = (Label)statsRow.Controls[3].Tag;

            // ---- Toolbar ----
            var toolbar = MakeToolbar("+ New Promotion", out var btnNew, out var btnRefresh);
            txtSearch = MakeSearchBox(56, 240);
            cboEligibleFilter = MakeFilterCombo(398, 130, "All", "Regular", "VIP", "PWD", "Senior");
            chkShowArchived = new CheckBox { Text = "Show archived", Left = 546, Top = 16, AutoSize = true };
            var btnAllEmails = MakePillButton("Email Log (all)", CardBg, TextDark, 900, 8, 150);
            btnAllEmails.Click += (s, e) => ShowEmailLog(selectedOnly: false);
            toolbar.Controls.AddRange(new Control[]
            {
                MakeToolbarLabel("Search", 0), txtSearch,
                MakeToolbarLabel("Eligible for", 312), cboEligibleFilter,
                chkShowArchived, btnAllEmails
            });

            // ---- Form card ----
            card = MakeFormCard(250, out lblFormTitle);

            int col1 = 24, col2 = col1 + LabelWidth + FieldWidth + ColGap;

            var lblCode = MakeFieldLabel("Promo Code", col1, 58);
            txtCode = MakeTextBox(col1 + LabelWidth, 58);
            txtCode.PlaceholderText = "e.g. SUMMER20";
            var lblDesc = MakeFieldLabel("Description", col1, 96);
            txtDescription = MakeTextBox(col1 + LabelWidth, 96);
            var lblDiscount = MakeFieldLabel("Discount %", col2, 58);
            txtDiscount = MakeTextBox(col2 + LabelWidth, 58);
            txtDiscount.PlaceholderText = "1 - 100";
            var lblEligible = MakeFieldLabel("Eligible For", col2, 96);
            cboEligibleCategory = MakeComboBox(col2 + LabelWidth, 96);
            cboEligibleCategory.Items.AddRange(new object[] { "All", "Regular", "VIP", "PWD", "Senior" });
            cboEligibleCategory.SelectedIndex = 0;
            chkActive = new CheckBox { Text = "Active", Left = col1, Top = 134, AutoSize = true, Checked = true };

            lblFormError = new Label { Left = col1, Top = 168, Width = 900, Height = 22, ForeColor = Danger, Font = new Font("Segoe UI", 9f, FontStyle.Bold), UseMnemonic = false };

            int btnTop = 196;
            btnSave = MakePillButton("Add Promotion", Accent, Color.White, col1, btnTop, 150);
            btnCancel = MakePillButton("Cancel", CardBg, TextMuted, col1 + 162, btnTop, 110);
            btnArchive = MakePillButton("Archive", Danger, Color.White, col1 + 286, btnTop, 130);
            btnArchive.Visible = false;
            btnEmail = MakePillButton("✉  Email Customers", AccentCoral, Color.White, col1 + 430, btnTop, 180);
            btnEmailLog = MakePillButton("Email Log", CardBg, TextDark, col1 + 622, btnTop, 120);
            btnEmail.Visible = false;
            btnEmailLog.Visible = false;

            card.Controls.AddRange(new Control[]
            {
                lblCode, txtCode, lblDesc, txtDescription, lblDiscount, txtDiscount,
                lblEligible, cboEligibleCategory, chkActive, lblFormError,
                btnSave, btnCancel, btnArchive, btnEmail, btnEmailLog
            });

            // ---- Validation rules ----
            _v = new FormValidator(lblFormError);
            _v.Watch(txtCode, () => InputRules.Code(txtCode.Text, "Promo code", 2, 20) ?? DuplicateCodeMessage());
            _v.Watch(txtDescription, () => InputRules.Text(txtDescription.Text, "Description", 3, 100, required: false));
            _v.Watch(txtDiscount, () => InputRules.Number(txtDiscount.Text, "Discount", 0.01m, 100m));
            _v.Watch(cboEligibleCategory, () => cboEligibleCategory.SelectedItem is null ? "Choose who the promotion is for." : null);

            var spacerMid = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = ContentBg };
            var spacerBottom = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = ContentBg };

            header.Controls.Add(spacerBottom);
            header.Controls.Add(card);
            header.Controls.Add(toolbar);
            header.Controls.Add(spacerMid);
            header.Controls.Add(statsRow);
            header.Controls.Add(topBar);

            lblInfo = new Label { Dock = DockStyle.Top, Height = 24, Text = "Ready.", ForeColor = TextMuted, Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), Padding = new Padding(0, 4, 0, 0) };

            // ---- Grid ----
            dgvPromotions = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvPromotions);
            AddTextColumn(dgvPromotions, nameof(PromotionRow.PromotionId), "ID", 40);
            AddTextColumn(dgvPromotions, nameof(PromotionRow.Code), "Code", 100);
            AddTextColumn(dgvPromotions, nameof(PromotionRow.Description), "Description", 200);
            AddTextColumn(dgvPromotions, nameof(PromotionRow.DiscountPercent), "Discount", 80);
            AddTextColumn(dgvPromotions, nameof(PromotionRow.EligibleCategory), "Eligible For", 100);
            AddTextColumn(dgvPromotions, nameof(PromotionRow.IsActive), "Active", 70);
            AddTextColumn(dgvPromotions, nameof(PromotionRow.IsArchived), "Archived", 70);
            dgvPromotions.Columns[nameof(PromotionRow.DiscountPercent)].DefaultCellStyle.Format = "0.##\\%";
            MakeBadgeColumn(dgvPromotions, nameof(PromotionRow.EligibleCategory), category => category switch
            {
                "VIP" => BadgeAmber,
                "PWD" => BadgeBlue,
                "Senior" => BadgeGreen,
                _ => BadgeGray
            });
            MakeYesNoColumn(dgvPromotions, nameof(PromotionRow.IsActive), BadgeGreen, BadgeGray);
            MakeYesNoColumn(dgvPromotions, nameof(PromotionRow.IsArchived), BadgeAmber, BadgeGray);

            // ---- Events ----
            btnNew.Click += (s, e) => OpenForm(null);
            btnRefresh.Click += async (s, e) => await LoadPromotionsAsync();
            btnSave.Click += async (s, e) => await SaveAsync();
            btnCancel.Click += (s, e) => CloseForm();
            btnArchive.Click += async (s, e) => await ArchiveAsync();
            btnEmail.Click += async (s, e) => await EmailCustomersAsync();
            btnEmailLog.Click += (s, e) => ShowEmailLog(selectedOnly: true);
            txtSearch.TextChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };
            cboEligibleFilter.SelectedIndexChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };
            chkShowArchived.CheckedChanged += async (s, e) => await LoadPromotionsAsync();
            dgvPromotions.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvPromotions.Rows[e.RowIndex].DataBoundItem is PromotionRow row)
                    OpenForm(row);
            };

            _pager = new Pager(dgvPromotions);

            Controls.Add(dgvPromotions);

            Controls.Add(_pager);
            Controls.Add(lblInfo);
            Controls.Add(header);
        }

        private string? DuplicateCodeMessage()
        {
            var code = txtCode.Text.Trim();
            if (code.Length == 0) return null;
            return _all.Any(p => p.PromotionId != _selectedId && p.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
                ? "That promo code already exists."
                : null;
        }

        // ------------------------------------------------------------ Data

        private async Task LoadPromotionsAsync()
        {
            try
            {
                lblInfo.Text = "Loading promotions…";
                var url = $"/tenant/{_companyId}/promotions?includeArchived={chkShowArchived.Checked}";
                var promos = await _http.GetFromJsonAsync<List<Promotion>>(url) ?? new();

                _all = promos.Select(p => new PromotionRow
                {
                    PromotionId = p.PromotionId,
                    Code = p.Code ?? "",
                    Description = p.Description ?? "",
                    DiscountPercent = p.DiscountPercent,
                    EligibleCategory = string.IsNullOrWhiteSpace(p.EligibleCategory) ? "All" : p.EligibleCategory,
                    IsActive = p.IsActive,
                    IsArchived = p.IsArchived
                }).ToList();

                var live = _all.Where(p => !p.IsArchived).ToList();
                statTotal.Text = live.Count.ToString();
                statActive.Text = live.Count(p => p.IsActive).ToString();
                statInactive.Text = live.Count(p => !p.IsActive).ToString();
                statHighest.Text = live.Count == 0 ? "-" : $"{live.Max(p => p.DiscountPercent):0.##}%";

                ApplyFilter();
            }
            catch (Exception ex)
            {
                lblInfo.Text = "Failed to load promotions.";
                MessageBox.Show($"Failed to load promotions: {ex.Message}");
            }
        }

        private void ApplyFilter()
        {
            var term = txtSearch.Text.Trim();
            var eligible = cboEligibleFilter.SelectedItem?.ToString() ?? "All";

            var filtered = _all.Where(p =>
                (eligible == "All" || p.EligibleCategory == eligible) &&
                (term.Length == 0 ||
                 p.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 p.Description.Contains(term, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            _pager.Bind(filtered);
            lblInfo.Text = $"Showing {filtered.Count} promotion(s). Click a row to edit it.";
        }

        // ------------------------------------------------------------ Form

        private void OpenForm(PromotionRow? row)
        {
            if (row is null)
            {
                ClearFields();
                lblFormTitle.Text = "New Promotion";
                btnSave.Text = "Add Promotion";
                btnArchive.Visible = false;
                btnEmail.Visible = false;
                btnEmailLog.Visible = false;
            }
            else
            {
                _selectedId = row.PromotionId;
                txtCode.Text = row.Code;
                txtDescription.Text = row.Description;
                txtDiscount.Text = row.DiscountPercent.ToString("0.##", CultureInfo.InvariantCulture);
                cboEligibleCategory.SelectedItem = cboEligibleCategory.Items.Contains(row.EligibleCategory) ? row.EligibleCategory : "All";
                chkActive.Checked = row.IsActive;
                lblFormTitle.Text = $"Edit Promotion #{row.PromotionId}";
                btnSave.Text = "Save Changes";
                btnArchive.Visible = !row.IsArchived;
                btnEmail.Visible = !row.IsArchived && row.IsActive;
                btnEmailLog.Visible = true;
            }

            _v.Reset();
            card.Visible = true;
            txtCode.Focus();
        }

        // ------------------------------------------------------------ Emails

        private class NotifyResultDto
        {
            public int Eligible { get; set; }
            public int AlreadySent { get; set; }
            public int ToSend { get; set; }
        }

        private async Task EmailCustomersAsync()
        {
            if (_selectedId is null) return;
            var promo = _all.FirstOrDefault(p => p.PromotionId == _selectedId);
            if (promo is null) return;

            var url = $"/tenant/{_companyId}/promotions/{_selectedId}/notify";
            try
            {
                // 1) Ask the server who would receive it
                using var previewResponse = await _http.PostAsJsonAsync(url, new { previewOnly = true });
                if (!previewResponse.IsSuccessStatusCode)
                {
                    MessageBox.Show(await previewResponse.Content.ReadAsStringAsync(), "Cannot email yet", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                var preview = await previewResponse.Content.ReadFromJsonAsync<NotifyResultDto>() ?? new();

                if (preview.ToSend == 0)
                {
                    MessageBox.Show(preview.Eligible == 0
                        ? $"No customers with an email address match \"{promo.EligibleCategory}\"."
                        : $"All {preview.Eligible} eligible customer(s) already received this promotion.",
                        "Nothing to send", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 2) Let the user edit the subject and add a note
                var defaultSubject = $"Special offer: {promo.DiscountPercent:0.##}% off with code {promo.Code}";
                using var compose = new EmailComposeForm(promo.Code, defaultSubject, preview.ToSend, preview.AlreadySent);
                if (compose.ShowDialog(FindForm()) != DialogResult.OK) return;

                // 3) Queue the emails; the server sends them in the background
                using var sendResponse = await _http.PostAsJsonAsync(url, new { subject = compose.Subject, message = compose.Message, previewOnly = false });
                if (!sendResponse.IsSuccessStatusCode)
                {
                    MessageBox.Show(await sendResponse.Content.ReadAsStringAsync(), "Not sent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                lblInfo.Text = $"{preview.ToSend} email(s) queued for promotion {promo.Code}.";
                ShowEmailLog(selectedOnly: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not reach the server: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowEmailLog(bool selectedOnly)
        {
            var promoId = selectedOnly ? _selectedId : null;
            var code = selectedOnly ? _all.FirstOrDefault(p => p.PromotionId == _selectedId)?.Code : null;
            using var log = new EmailLogForm(_http, _companyId, promoId, code is null ? "Email log" : $"Email log: {code}");
            log.ShowDialog(FindForm());
        }

        private void CloseForm()
        {
            ClearFields();
            _v.Reset();
            card.Visible = false;
        }

        private void ClearFields()
        {
            _selectedId = null;
            txtCode.Clear();
            txtDescription.Clear();
            txtDiscount.Clear();
            cboEligibleCategory.SelectedIndex = 0;
            chkActive.Checked = true;
        }

        private async Task SaveAsync()
        {
            if (!_v.ValidateAll()) return;

            var promo = new Promotion
            {
                Code = txtCode.Text.Trim(),
                Description = txtDescription.Text.Trim(),
                DiscountPercent = decimal.Parse(txtDiscount.Text.Trim(), CultureInfo.InvariantCulture),
                EligibleCategory = cboEligibleCategory.SelectedItem?.ToString() ?? "All",
                IsActive = chkActive.Checked
            };

            var adding = _selectedId is null;
            var response = adding
                ? await _http.PostAsJsonAsync($"/tenant/{_companyId}/promotions", promo)
                : await _http.PutAsJsonAsync($"/tenant/{_companyId}/promotions/{_selectedId}", promo);

            if (response.IsSuccessStatusCode)
            {
                lblInfo.Text = adding ? "Promotion added." : "Promotion updated.";
                CloseForm();
                await LoadPromotionsAsync();
            }
            else
            {
                lblInfo.Text = adding ? "Add failed." : "Update failed.";
                MessageBox.Show($"Failed to save promotion: {response.StatusCode}\n{await response.Content.ReadAsStringAsync()}");
            }
        }

        private async Task ArchiveAsync()
        {
            if (_selectedId is null) return;
            if (MessageBox.Show("Archive this promotion?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            var response = await _http.PutAsync($"/tenant/{_companyId}/promotions/{_selectedId}/archive", null);
            if (response.IsSuccessStatusCode)
            {
                lblInfo.Text = "Promotion archived.";
                CloseForm();
                await LoadPromotionsAsync();
            }
            else
            {
                lblInfo.Text = "Archive failed.";
                MessageBox.Show($"Failed to archive promotion: {response.StatusCode}");
            }
        }
    }
}