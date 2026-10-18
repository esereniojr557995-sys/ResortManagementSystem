using System.Net.Http.Json;
using System.Drawing;
using ResortManagementSystem.Domain.Entities;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class StaffControl : UserControl
    {
        private readonly HttpClient _http;
        private readonly int _companyId;

        private DataGridView dgvStaff = null!;

        private Pager _pager = null!;
        private TextBox txtFullName = null!, txtEmail = null!, txtPhone = null!, txtPassword = null!, txtSearch = null!;
        private ComboBox cboRole = null!, cboRoleFilter = null!;
        private CheckBox chkActive = null!, chkShowArchived = null!;
        private Label lblInfo = null!, lblFormTitle = null!, lblFormError = null!;
        private Panel card = null!;
        private Button btnSave = null!, btnCancel = null!, btnArchive = null!;
        private Label statTotal = null!, statActive = null!, statManagers = null!, statStaff = null!;
        private FormValidator _v = null!;

        private int? _selectedStaffId = null;
        private List<Staff> _all = new();
        private Dictionary<int, string> _branchNames = new();
        private DataGridViewColumn? _branchCol;

        public StaffControl(HttpClient http, int companyId)
        {
            _http = http;
            _companyId = companyId;
            Dock = DockStyle.Fill;
            BackColor = ContentBg;
            Padding = new Padding(36, 28, 36, 28);
            BuildUi();
        }

        public async Task LoadAsync() => await LoadStaffAsync();

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = ContentBg };

            var topBar = BuildTopBar("Staff & Manager Accounts", "Manage employee accounts and roles", out _);

            var statsRow = MakeStatsRow(
                ("Total Accounts", Accent, "not archived"),
                ("Active", Accent, "can sign in"),
                ("Managers", ChartBlue, "manager role"),
                ("Front Desk Staff", ChartGray, "staff role")
            );
            statTotal = (Label)statsRow.Controls[0].Tag;
            statActive = (Label)statsRow.Controls[1].Tag;
            statManagers = (Label)statsRow.Controls[2].Tag;
            statStaff = (Label)statsRow.Controls[3].Tag;

            // ---- Toolbar ----
            var toolbar = MakeToolbar("+ New Account", out var btnNew, out var btnRefresh);
            txtSearch = MakeSearchBox(56, 240);
            cboRoleFilter = MakeFilterCombo(366, 130, "All roles", "Staff", "Manager", "Admin");
            chkShowArchived = new CheckBox { Text = "Show archived", Left = 514, Top = 16, AutoSize = true };
            toolbar.Controls.AddRange(new Control[]
            {
                MakeToolbarLabel("Search", 0), txtSearch,
                MakeToolbarLabel("Role", 312), cboRoleFilter,
                chkShowArchived
            });

            // ---- Form card ----
            card = MakeFormCard(252, out lblFormTitle);

            int col1 = 24, col2 = col1 + LabelWidth + FieldWidth + ColGap;

            var lblFullName = MakeFieldLabel("Full Name", col1, 58);
            txtFullName = MakeTextBox(col1 + LabelWidth, 58);
            var lblEmail = MakeFieldLabel("Email", col1, 96);
            txtEmail = MakeTextBox(col1 + LabelWidth, 96);
            var lblPhone = MakeFieldLabel("Phone", col2, 58);
            txtPhone = MakeTextBox(col2 + LabelWidth, 58);
            txtPhone.PlaceholderText = "09171234567";
            var lblRole = MakeFieldLabel("Role", col2, 96);
            cboRole = MakeComboBox(col2 + LabelWidth, 96);
            cboRole.Items.AddRange(new object[] { "Staff", "Manager", "Admin" });
            cboRole.SelectedIndex = 0;

            chkActive = new CheckBox { Text = "Active", Left = col1, Top = 134, AutoSize = true, Checked = true };
            var lblPassword = MakeFieldLabel("Password", col2, 134);
            txtPassword = MakeTextBox(col2 + LabelWidth, 134);
            txtPassword.UseSystemPasswordChar = true;

            lblFormError = new Label { Left = col1, Top = 170, Width = 900, Height = 22, ForeColor = Danger, Font = new Font("Segoe UI", 9f, FontStyle.Bold), UseMnemonic = false };

            int btnTop = 198;
            btnSave = MakePillButton("Add Account", Accent, Color.White, col1, btnTop, 150);
            btnCancel = MakePillButton("Cancel", CardBg, TextMuted, col1 + 162, btnTop, 110);
            btnArchive = MakePillButton("Archive", Danger, Color.White, col1 + 286, btnTop, 130);
            btnArchive.Visible = false;

            card.Controls.AddRange(new Control[]
            {
                lblFullName, txtFullName, lblEmail, txtEmail, lblPhone, txtPhone,
                lblRole, cboRole, chkActive, lblPassword, txtPassword, lblFormError,
                btnSave, btnCancel, btnArchive
            });

            // ---- Validation rules ----
            _v = new FormValidator(lblFormError);
            _v.Watch(txtFullName, () => InputRules.PersonName(txtFullName.Text));
            _v.Watch(txtEmail, () => InputRules.Email(txtEmail.Text) ?? DuplicateEmailMessage());
            _v.Watch(txtPhone, () => InputRules.Phone(txtPhone.Text, required: false));
            _v.Watch(cboRole, () => cboRole.SelectedItem is null ? "Select a role." : null);
            // New account: password required. Editing: leave blank to keep the current one.
            _v.Watch(txtPassword, () => InputRules.Password(txtPassword.Text, required: _selectedStaffId is null));

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
            dgvStaff = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvStaff);
            AddTextColumn(dgvStaff, nameof(Staff.StaffId), "ID", 40);
            AddTextColumn(dgvStaff, nameof(Staff.FullName), "Full Name", 140);
            AddTextColumn(dgvStaff, nameof(Staff.Email), "Email", 170);
            AddTextColumn(dgvStaff, nameof(Staff.PhoneNumber), "Phone", 110);
            AddTextColumn(dgvStaff, nameof(Staff.Role), "Role", 90);

            // Read-only: assign people to branches from Branch Management > Manage Team
            _branchCol = new DataGridViewTextBoxColumn
            {
                HeaderText = "Branch",
                DataPropertyName = nameof(Staff.BranchId),
                Width = 130,
                FillWeight = 110,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            dgvStaff.Columns.Add(_branchCol);
            AddTextColumn(dgvStaff, nameof(Staff.IsActive), "Active", 70);
            AddTextColumn(dgvStaff, nameof(Staff.IsArchived), "Archived", 70);
            MakeBadgeColumn(dgvStaff, nameof(Staff.Role), role => role switch
            {
                "Admin" => BadgeBlue,
                "Manager" => BadgeGreen,
                _ => BadgeGray
            });
            MakeYesNoColumn(dgvStaff, nameof(Staff.IsActive), BadgeGreen, BadgeGray);
            MakeYesNoColumn(dgvStaff, nameof(Staff.IsArchived), BadgeAmber, BadgeGray);

            // ---- Events ----
            dgvStaff.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || _branchCol is null || e.ColumnIndex != _branchCol.Index) return;
                if (dgvStaff.Rows[e.RowIndex].DataBoundItem is not Staff st) return;

                e.Value = st.BranchId is int bid
                    ? (_branchNames.TryGetValue(bid, out var name) ? name : $"Branch #{bid}")
                    : (st.Role == "Admin" ? "All branches" : "Unassigned");
                e.FormattingApplied = true;
            };
            btnNew.Click += (s, e) => OpenForm(null);
            btnRefresh.Click += async (s, e) => await LoadStaffAsync();
            btnSave.Click += async (s, e) => await SaveAsync();
            btnCancel.Click += (s, e) => CloseForm();
            btnArchive.Click += async (s, e) => await ArchiveAsync();
            txtSearch.TextChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };
            cboRoleFilter.SelectedIndexChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };
            chkShowArchived.CheckedChanged += async (s, e) => await LoadStaffAsync();
            dgvStaff.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvStaff.Rows[e.RowIndex].DataBoundItem is Staff row)
                    OpenForm(row);
            };

            _pager = new Pager(dgvStaff);

            Controls.Add(dgvStaff);

            Controls.Add(_pager);
            Controls.Add(lblInfo);
            Controls.Add(header);
        }

        private string? DuplicateEmailMessage()
        {
            var email = txtEmail.Text.Trim();
            if (email.Length == 0) return null;
            return _all.Any(s => s.StaffId != _selectedStaffId && (s.Email ?? "").Equals(email, StringComparison.OrdinalIgnoreCase))
                ? "Another account already uses this email address."
                : null;
        }

        // ------------------------------------------------------------ Data

        private async Task LoadStaffAsync()
        {
            try
            {
                lblInfo.Text = "Loading staff…";
                var url = $"/tenant/{_companyId}/staff?includeArchived={chkShowArchived.Checked}";
                _all = await _http.GetFromJsonAsync<List<Staff>>(url) ?? new();

                // Branch names are a nicety for the Branch column: the list still works without them
                try
                {
                    var branches = await _http.GetFromJsonAsync<List<Branch>>($"/tenant/{_companyId}/branches?includeArchived=true") ?? new();
                    _branchNames = branches.ToDictionary(b => b.BranchId, b => b.BranchName);
                }
                catch { }

                var live = _all.Where(s => !s.IsArchived).ToList();
                statTotal.Text = live.Count.ToString();
                statActive.Text = live.Count(s => s.IsActive).ToString();
                statManagers.Text = live.Count(s => s.Role == "Manager").ToString();
                statStaff.Text = live.Count(s => s.Role == "Staff").ToString();

                ApplyFilter();
            }
            catch (Exception ex)
            {
                lblInfo.Text = "Failed to load staff.";
                MessageBox.Show($"Failed to load staff: {ex.Message}");
            }
        }

        private void ApplyFilter()
        {
            var term = txtSearch.Text.Trim();
            var role = cboRoleFilter.SelectedItem?.ToString() ?? "All roles";

            var filtered = _all.Where(s =>
                (role == "All roles" || s.Role == role) &&
                (term.Length == 0 ||
                 (s.FullName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 (s.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 (s.PhoneNumber ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            _pager.Bind(filtered);
            lblInfo.Text = $"Showing {filtered.Count} account(s). Click a row to edit it.";
        }

        // ------------------------------------------------------------ Form

        private void OpenForm(Staff? row)
        {
            if (row is null)
            {
                ClearFields();
                lblFormTitle.Text = "New Staff Account";
                btnSave.Text = "Add Account";
                btnArchive.Visible = false;
                txtPassword.PlaceholderText = "At least 8 characters, letters and numbers";
            }
            else
            {
                _selectedStaffId = row.StaffId;
                txtFullName.Text = row.FullName;
                txtEmail.Text = row.Email;
                txtPhone.Text = row.PhoneNumber;
                cboRole.SelectedItem = row.Role;
                chkActive.Checked = row.IsActive;
                txtPassword.Clear();   // passwords are never loaded back; only a hash exists
                txtPassword.PlaceholderText = "Leave blank to keep the current password";
                lblFormTitle.Text = $"Edit Account #{row.StaffId}";
                btnSave.Text = "Save Changes";
                btnArchive.Visible = !row.IsArchived;
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
            _selectedStaffId = null;
            txtFullName.Clear();
            txtEmail.Clear();
            txtPhone.Clear();
            txtPassword.Clear();
            cboRole.SelectedIndex = 0;
            chkActive.Checked = true;
        }

        private async Task SaveAsync()
        {
            if (!_v.ValidateAll()) return;

            var body = new
            {
                fullName = txtFullName.Text.Trim(),
                email = txtEmail.Text.Trim(),
                phoneNumber = txtPhone.Text.Trim(),
                role = cboRole.SelectedItem?.ToString() ?? "Staff",
                isActive = chkActive.Checked,
                password = string.IsNullOrEmpty(txtPassword.Text) ? null : txtPassword.Text
            };

            var adding = _selectedStaffId is null;
            var response = adding
                ? await _http.PostAsJsonAsync($"/tenant/{_companyId}/staff", body)
                : await _http.PutAsJsonAsync($"/tenant/{_companyId}/staff/{_selectedStaffId}", body);

            if (response.IsSuccessStatusCode)
            {
                lblInfo.Text = adding
                    ? "Account added. They can now sign in with this email and password."
                    : "Account updated.";
                CloseForm();
                await LoadStaffAsync();
            }
            else
            {
                lblInfo.Text = adding ? "Add failed." : "Update failed.";
                MessageBox.Show($"Failed to save account:\n{await response.Content.ReadAsStringAsync()}");
            }
        }

        private async Task ArchiveAsync()
        {
            if (_selectedStaffId is null) return;
            if (MessageBox.Show("Archive this account? They will no longer be able to sign in.", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            var response = await _http.PutAsync($"/tenant/{_companyId}/staff/{_selectedStaffId}/archive", null);
            if (response.IsSuccessStatusCode)
            {
                lblInfo.Text = "Account archived.";
                CloseForm();
                await LoadStaffAsync();
            }
            else
            {
                lblInfo.Text = "Archive failed.";
                MessageBox.Show($"Failed to archive account: {response.StatusCode}");
            }
        }
    }
}