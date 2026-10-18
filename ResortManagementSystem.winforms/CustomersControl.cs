using System.Net.Http.Json;
using System.Drawing;
using ResortManagementSystem.Domain.Entities;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class CustomersControl : UserControl
    {
        private readonly HttpClient _http;
        private readonly int _companyId;

        private DataGridView dgvCustomers = null!;

        private Pager _pager = null!;
        private TextBox txtFullName = null!, txtEmail = null!, txtPhone = null!, txtAddress = null!;
        private ComboBox cboCategory = null!, cboCategoryFilter = null!;
        private TextBox txtSearch = null!;
        private CheckBox chkShowArchived = null!;
        private Label lblStatus = null!, lblFormTitle = null!, lblFormError = null!;
        private Panel card = null!;
        private Button btnSave = null!, btnCancel = null!, btnArchive = null!, btnRestore = null!;
        private Label statTotal = null!, statWeek = null!, statPriority = null!, statMissing = null!;
        private FormValidator _v = null!;

        private int? _selectedCustomerId = null;
        private List<CustomerRow> _all = new();

        // What the grid shows. A blank category is displayed as "Regular".
        private class CustomerRow
        {
            public int CustomerId { get; set; }
            public string FullName { get; set; } = "";
            public string Email { get; set; } = "";
            public string PhoneNumber { get; set; } = "";
            public string Address { get; set; } = "";
            public string Category { get; set; } = "Regular";
            public bool IsArchived { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public CustomersControl(HttpClient http, int companyId)
        {
            _http = http;
            _companyId = companyId;
            Dock = DockStyle.Fill;
            BackColor = ContentBg;
            Padding = new Padding(36, 28, 36, 28);
            BuildUi();
        }

        public async Task LoadAsync() => await LoadCustomersAsync();

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = ContentBg };

            var topBar = BuildTopBar("Customers", "Guest profiles and contact records", out _);

            var statsRow = MakeStatsRow(
                ("Total Customers", Accent, "active guest profiles"),
                ("New This Week", Accent, "added in the last 7 days"),
                ("Priority Guests", ChartBlue, "VIP, PWD and Senior"),
                ("Missing Contact Info", AccentAmber, "no phone or address")
            );
            statTotal = (Label)statsRow.Controls[0].Tag;
            statWeek = (Label)statsRow.Controls[1].Tag;
            statPriority = (Label)statsRow.Controls[2].Tag;
            statMissing = (Label)statsRow.Controls[3].Tag;

            // ---- Toolbar ----
            var toolbar = MakeToolbar("+ New Customer", out var btnNew, out var btnRefresh);
            txtSearch = MakeSearchBox(56, 240);
            cboCategoryFilter = MakeFilterCombo(378, 130, "All", "Regular", "VIP", "PWD", "Senior");
            chkShowArchived = new CheckBox { Text = "Show archived", Left = 526, Top = 16, AutoSize = true };
            toolbar.Controls.AddRange(new Control[]
            {
                MakeToolbarLabel("Search", 0), txtSearch,
                MakeToolbarLabel("Category", 312), cboCategoryFilter,
                chkShowArchived
            });

            // ---- Form card ----
            card = MakeFormCard(256, out lblFormTitle);

            int col1 = 24, col2 = col1 + LabelWidth + FieldWidth + ColGap;

            var lblFullName = MakeFieldLabel("Full Name", col1, 58);
            txtFullName = MakeTextBox(col1 + LabelWidth, 58);
            var lblEmail = MakeFieldLabel("Email", col1, 96);
            txtEmail = MakeTextBox(col1 + LabelWidth, 96);
            var lblPhone = MakeFieldLabel("Phone", col2, 58);
            txtPhone = MakeTextBox(col2 + LabelWidth, 58);
            txtPhone.PlaceholderText = "09171234567";
            var lblAddress = MakeFieldLabel("Address", col2, 96);
            txtAddress = MakeTextBox(col2 + LabelWidth, 96);
            var lblCategory = MakeFieldLabel("Category", col1, 134);
            cboCategory = MakeComboBox(col1 + LabelWidth, 134);
            cboCategory.Items.AddRange(new object[] { "Regular", "VIP", "PWD", "Senior" });
            cboCategory.SelectedIndex = 0;

            lblFormError = new Label { Left = col1, Top = 172, Width = 900, Height = 22, ForeColor = Danger, Font = new Font("Segoe UI", 9f, FontStyle.Bold), UseMnemonic = false };

            int btnTop = 200;
            btnSave = MakePillButton("Add Customer", Accent, Color.White, col1, btnTop, 150);
            btnCancel = MakePillButton("Cancel", CardBg, TextMuted, col1 + 162, btnTop, 110);
            btnArchive = MakePillButton("Archive", Danger, Color.White, col1 + 286, btnTop, 130);
            btnRestore = MakePillButton("Restore", TextDark, Color.White, col1 + 286, btnTop, 130);
            btnArchive.Visible = false;
            btnRestore.Visible = false;

            card.Controls.AddRange(new Control[]
            {
                lblFullName, txtFullName, lblEmail, txtEmail, lblPhone, txtPhone,
                lblAddress, txtAddress, lblCategory, cboCategory, lblFormError,
                btnSave, btnCancel, btnArchive, btnRestore
            });

            // ---- Validation rules ----
            _v = new FormValidator(lblFormError);
            _v.Watch(txtFullName, () => InputRules.PersonName(txtFullName.Text));
            _v.Watch(txtEmail, () => InputRules.Email(txtEmail.Text) ?? DuplicateEmailMessage());
            _v.Watch(txtPhone, () => InputRules.Phone(txtPhone.Text, required: false));
            _v.Watch(txtAddress, () => InputRules.Text(txtAddress.Text, "Address", 3, 150, required: false));

            var spacerMid = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = ContentBg };
            var spacerBottom = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = ContentBg };

            // Dock = Top: the LAST control added ends up on top
            header.Controls.Add(spacerBottom);
            header.Controls.Add(card);
            header.Controls.Add(toolbar);
            header.Controls.Add(spacerMid);
            header.Controls.Add(statsRow);
            header.Controls.Add(topBar);

            lblStatus = new Label { Dock = DockStyle.Top, Height = 24, Text = "Ready.", ForeColor = TextMuted, Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), Padding = new Padding(0, 4, 0, 0) };

            // ---- Grid ----
            dgvCustomers = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvCustomers);
            AddTextColumn(dgvCustomers, nameof(CustomerRow.CustomerId), "ID", 40);
            AddTextColumn(dgvCustomers, nameof(CustomerRow.FullName), "Full Name", 130);
            AddTextColumn(dgvCustomers, nameof(CustomerRow.Email), "Email", 160);
            AddTextColumn(dgvCustomers, nameof(CustomerRow.PhoneNumber), "Phone", 110);
            AddTextColumn(dgvCustomers, nameof(CustomerRow.Address), "Address", 140);
            AddTextColumn(dgvCustomers, nameof(CustomerRow.Category), "Category", 90);
            AddTextColumn(dgvCustomers, nameof(CustomerRow.IsArchived), "Archived", 70);
            AddTextColumn(dgvCustomers, nameof(CustomerRow.CreatedAt), "Created", 120);
            dgvCustomers.Columns[nameof(CustomerRow.CreatedAt)].DefaultCellStyle.Format = "MM/dd/yyyy h:mm tt";
            MakeBadgeColumn(dgvCustomers, nameof(CustomerRow.Category), category => category switch
            {
                "VIP" => BadgeAmber,
                "PWD" => BadgeBlue,
                "Senior" => BadgeGreen,
                _ => BadgeGray
            });
            MakeYesNoColumn(dgvCustomers, nameof(CustomerRow.IsArchived), BadgeAmber, BadgeGray);

            // ---- Events ----
            btnNew.Click += (s, e) => OpenForm(null);
            btnRefresh.Click += async (s, e) => await LoadCustomersAsync();
            btnSave.Click += async (s, e) => await SaveAsync();
            btnCancel.Click += (s, e) => CloseForm();
            btnArchive.Click += async (s, e) => await ArchiveAsync();
            btnRestore.Click += async (s, e) => await RestoreAsync();
            txtSearch.TextChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };
            cboCategoryFilter.SelectedIndexChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };
            chkShowArchived.CheckedChanged += async (s, e) => await LoadCustomersAsync();
            dgvCustomers.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvCustomers.Rows[e.RowIndex].DataBoundItem is CustomerRow row)
                    OpenForm(row);
            };

            _pager = new Pager(dgvCustomers);

            Controls.Add(dgvCustomers);

            Controls.Add(_pager);
            Controls.Add(lblStatus);
            Controls.Add(header);
        }

        private string? DuplicateEmailMessage()
        {
            var email = txtEmail.Text.Trim();
            if (email.Length == 0) return null;
            return _all.Any(c => c.CustomerId != _selectedCustomerId && c.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
                ? "Another customer already uses this email address."
                : null;
        }

        // ------------------------------------------------------------ Data

        private async Task LoadCustomersAsync()
        {
            try
            {
                lblStatus.Text = "Loading customers…";
                var url = $"/tenant/{_companyId}/customers?includeArchived={chkShowArchived.Checked}";
                var customers = await _http.GetFromJsonAsync<List<Customer>>(url) ?? new();

                _all = customers.Select(c => new CustomerRow
                {
                    CustomerId = c.CustomerId,
                    FullName = c.FullName ?? "",
                    Email = c.Email ?? "",
                    PhoneNumber = c.PhoneNumber ?? "",
                    Address = c.Address ?? "",
                    Category = string.IsNullOrWhiteSpace(c.CustomerCategory) ? "Regular" : c.CustomerCategory,
                    IsArchived = c.IsArchived,
                    CreatedAt = c.CreatedAt
                }).ToList();

                var active = _all.Where(c => !c.IsArchived).ToList();
                statTotal.Text = active.Count.ToString();
                statWeek.Text = active.Count(c => c.CreatedAt >= DateTime.UtcNow.AddDays(-7)).ToString();
                statPriority.Text = active.Count(c => c.Category is "VIP" or "PWD" or "Senior").ToString();
                statMissing.Text = active.Count(c => string.IsNullOrWhiteSpace(c.PhoneNumber) || string.IsNullOrWhiteSpace(c.Address)).ToString();

                ApplyFilter();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Failed to load customers.";
                MessageBox.Show($"Failed to load customers: {ex.Message}");
            }
        }

        private void ApplyFilter()
        {
            var term = txtSearch.Text.Trim();
            var category = cboCategoryFilter.SelectedItem?.ToString() ?? "All";

            var filtered = _all.Where(c =>
                (category == "All" || c.Category == category) &&
                (term.Length == 0 ||
                 c.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 c.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 c.PhoneNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 c.Address.Contains(term, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            _pager.Bind(filtered);
            lblStatus.Text = $"Showing {filtered.Count} customer(s). Click a row to edit it.";
        }

        // ------------------------------------------------------------ Form

        private void OpenForm(CustomerRow? row)
        {
            if (row is null)
            {
                ClearFields();
                lblFormTitle.Text = "New Customer";
                btnSave.Text = "Add Customer";
                btnArchive.Visible = false;
                btnRestore.Visible = false;
            }
            else
            {
                _selectedCustomerId = row.CustomerId;
                txtFullName.Text = row.FullName;
                txtEmail.Text = row.Email;
                txtPhone.Text = row.PhoneNumber;
                txtAddress.Text = row.Address;
                cboCategory.SelectedItem = cboCategory.Items.Contains(row.Category) ? row.Category : "Regular";
                lblFormTitle.Text = $"Edit Customer #{row.CustomerId}";
                btnSave.Text = "Save Changes";
                btnArchive.Visible = !row.IsArchived;
                btnRestore.Visible = row.IsArchived;
            }

            _v.Reset();
            card.Visible = true;
            txtFullName.Focus();
        }

        private void CloseForm()
        {
            ClearFields();
            _v.Reset();
            card.Visible = false;
        }

        private void ClearFields()
        {
            _selectedCustomerId = null;
            txtFullName.Clear();
            txtEmail.Clear();
            txtPhone.Clear();
            txtAddress.Clear();
            cboCategory.SelectedIndex = 0;
        }

        private async Task SaveAsync()
        {
            if (!_v.ValidateAll()) return;

            var customer = new Customer
            {
                FullName = txtFullName.Text.Trim(),
                Email = txtEmail.Text.Trim(),
                PhoneNumber = txtPhone.Text.Trim(),
                Address = txtAddress.Text.Trim(),
                CustomerCategory = cboCategory.SelectedItem?.ToString() ?? "Regular"
            };

            var adding = _selectedCustomerId is null;
            var response = adding
                ? await _http.PostAsJsonAsync($"/tenant/{_companyId}/customers", customer)
                : await _http.PutAsJsonAsync($"/tenant/{_companyId}/customers/{_selectedCustomerId}", customer);

            if (response.IsSuccessStatusCode)
            {
                lblStatus.Text = adding ? "Customer added." : "Customer updated.";
                CloseForm();
                await LoadCustomersAsync();
            }
            else
            {
                lblStatus.Text = adding ? "Add failed." : "Update failed.";
                MessageBox.Show($"Failed to save customer: {response.StatusCode}\n{await response.Content.ReadAsStringAsync()}");
            }
        }

        private async Task ArchiveAsync()
        {
            if (_selectedCustomerId is null) return;
            if (MessageBox.Show("Archive this customer? They'll be hidden from the active list but their history is preserved.", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            var response = await _http.PutAsync($"/tenant/{_companyId}/customers/{_selectedCustomerId}/archive", null);
            if (response.IsSuccessStatusCode)
            {
                lblStatus.Text = "Customer archived.";
                CloseForm();
                await LoadCustomersAsync();
            }
            else
            {
                lblStatus.Text = "Archive failed.";
                MessageBox.Show($"Failed to archive customer: {response.StatusCode}");
            }
        }

        private async Task RestoreAsync()
        {
            if (_selectedCustomerId is null) return;

            var response = await _http.PutAsync($"/tenant/{_companyId}/customers/{_selectedCustomerId}/unarchive", null);
            if (response.IsSuccessStatusCode)
            {
                lblStatus.Text = "Customer restored.";
                CloseForm();
                await LoadCustomersAsync();
            }
            else
            {
                lblStatus.Text = "Restore failed.";
                MessageBox.Show($"Failed to restore customer: {response.StatusCode}");
            }
        }
    }
}