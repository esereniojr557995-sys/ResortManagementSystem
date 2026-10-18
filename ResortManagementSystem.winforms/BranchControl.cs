using System.Net.Http.Json;
using System.Drawing;
using ResortManagementSystem.Domain.Entities;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class BranchControl : UserControl
    {
        private readonly HttpClient _http;
        private readonly int _companyId;

        private DataGridView dgvBranches = null!;

        private Pager _pager = null!;
        private TextBox txtName = null!, txtLocation = null!, txtSearch = null!;
        private CheckBox chkActive = null!, chkShowArchived = null!;
        private Label lblInfo = null!, lblFormTitle = null!, lblFormError = null!, lblTeam = null!;
        private Panel card = null!;
        private Button btnSave = null!, btnCancel = null!, btnArchive = null!, btnTeam = null!;
        private Label statTotal = null!, statActive = null!, statInactive = null!;
        private FormValidator _v = null!;

        private int? _selectedId = null;
        private List<Branch> _all = new();
        private List<Staff> _staff = new();

        // One grid row: the branch plus who runs it and how big its team is
        private class BranchRow
        {
            public Branch Source { get; set; } = null!;
            public int BranchId { get; set; }
            public string BranchName { get; set; } = "";
            public string Location { get; set; } = "";
            public string Manager { get; set; } = "";
            public int TeamSize { get; set; }
            public bool IsActive { get; set; }
            public bool IsArchived { get; set; }
        }

        public BranchControl(HttpClient http, int companyId)
        {
            _http = http;
            _companyId = companyId;
            Dock = DockStyle.Fill;
            BackColor = ContentBg;
            Padding = new Padding(36, 28, 36, 28);
            BuildUi();
        }

        public async Task LoadAsync() => await LoadBranchesAsync();

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = ContentBg };
            var topBar = BuildTopBar("Branch Management", "Multiple locations under this account (Medium-tier feature)", out _);

            var statsRow = MakeStatsRow(
                ("Total Branches", Accent, "not archived"),
                ("Active", Accent, "open for business"),
                ("Inactive", AccentAmber, "temporarily closed")
            );
            statTotal = (Label)statsRow.Controls[0].Tag;
            statActive = (Label)statsRow.Controls[1].Tag;
            statInactive = (Label)statsRow.Controls[2].Tag;

            // ---- Toolbar ----
            var toolbar = MakeToolbar("+ New Branch", out var btnNew, out var btnRefresh);
            txtSearch = MakeSearchBox(56, 240);
            chkShowArchived = new CheckBox { Text = "Show archived", Left = 326, Top = 16, AutoSize = true };
            toolbar.Controls.AddRange(new Control[] { MakeToolbarLabel("Search", 0), txtSearch, chkShowArchived });

            // ---- Form card ----
            card = MakeFormCard(210, out lblFormTitle);

            int col1 = 24, col2 = col1 + LabelWidth + FieldWidth + ColGap;

            var lblName = MakeFieldLabel("Branch Name", col1, 58);
            txtName = MakeTextBox(col1 + LabelWidth, 58);
            var lblLocation = MakeFieldLabel("Location", col2, 58);
            txtLocation = MakeTextBox(col2 + LabelWidth, 58);
            chkActive = new CheckBox { Text = "Active", Left = col1, Top = 96, AutoSize = true, Checked = true };

            // Who runs this branch and how many people work there (edit mode only)
            lblTeam = new Label
            {
                Left = col2,
                Top = 98,
                Width = 640,
                Height = 22,
                ForeColor = AccentDark,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                AutoEllipsis = true,
                UseMnemonic = false,
                Visible = false
            };

            lblFormError = new Label { Left = col1, Top = 130, Width = 900, Height = 22, ForeColor = Danger, Font = new Font("Segoe UI", 9f, FontStyle.Bold), UseMnemonic = false };

            int btnTop = 158;
            btnSave = MakePillButton("Add Branch", Accent, Color.White, col1, btnTop, 150);
            btnCancel = MakePillButton("Cancel", CardBg, TextMuted, col1 + 162, btnTop, 110);
            btnArchive = MakePillButton("Archive", Danger, Color.White, col1 + 286, btnTop, 130);
            btnArchive.Visible = false;
            btnTeam = MakePillButton("Manage Team", TextDark, Color.White, col1 + 430, btnTop, 150);
            btnTeam.Visible = false;

            card.Controls.AddRange(new Control[]
            {
                lblName, txtName, lblLocation, txtLocation, chkActive, lblTeam, lblFormError,
                btnSave, btnCancel, btnArchive, btnTeam
            });

            // ---- Validation rules ----
            _v = new FormValidator(lblFormError);
            _v.Watch(txtName, () => InputRules.Place(txtName.Text, "Branch name", 2, 100, required: true) ?? DuplicateNameMessage());
            _v.Watch(txtLocation, () => InputRules.Place(txtLocation.Text, "Location", 3, 150, required: true));

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
            dgvBranches = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvBranches);
            AddTextColumn(dgvBranches, nameof(BranchRow.BranchId), "ID", 40);
            AddTextColumn(dgvBranches, nameof(BranchRow.BranchName), "Branch Name", 180);
            AddTextColumn(dgvBranches, nameof(BranchRow.Location), "Location", 170);
            AddTextColumn(dgvBranches, nameof(BranchRow.Manager), "Branch Manager", 160);
            AddTextColumn(dgvBranches, nameof(BranchRow.TeamSize), "Team", 60);
            AddTextColumn(dgvBranches, nameof(BranchRow.IsActive), "Active", 70);
            AddTextColumn(dgvBranches, nameof(BranchRow.IsArchived), "Archived", 70);
            MakeYesNoColumn(dgvBranches, nameof(BranchRow.IsActive), BadgeGreen, BadgeGray);
            MakeYesNoColumn(dgvBranches, nameof(BranchRow.IsArchived), BadgeAmber, BadgeGray);

            // ---- Events ----
            btnNew.Click += (s, e) => OpenForm(null);
            btnRefresh.Click += async (s, e) => await LoadBranchesAsync();
            btnSave.Click += async (s, e) => await SaveAsync();
            btnCancel.Click += (s, e) => CloseForm();
            btnArchive.Click += async (s, e) => await ArchiveAsync();
            btnTeam.Click += async (s, e) => await OpenTeamAsync();
            txtSearch.TextChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };
            chkShowArchived.CheckedChanged += async (s, e) => await LoadBranchesAsync();
            dgvBranches.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvBranches.Rows[e.RowIndex].DataBoundItem is BranchRow row)
                    OpenForm(row.Source);
            };

            _pager = new Pager(dgvBranches);

            Controls.Add(dgvBranches);

            Controls.Add(_pager);
            Controls.Add(lblInfo);
            Controls.Add(header);
        }

        private string? DuplicateNameMessage()
        {
            var name = txtName.Text.Trim();
            if (name.Length == 0) return null;
            return _all.Any(b => b.BranchId != _selectedId && !b.IsArchived && b.BranchName.Equals(name, StringComparison.OrdinalIgnoreCase))
                ? "A branch with this name already exists."
                : null;
        }

        // ------------------------------------------------------------ Data

        private async Task LoadBranchesAsync()
        {
            try
            {
                lblInfo.Text = "Loading branches…";
                var url = $"/tenant/{_companyId}/branches?includeArchived={chkShowArchived.Checked}";
                _all = await _http.GetFromJsonAsync<List<Branch>>(url) ?? new();

                // Staff are only needed for the Manager / Team columns, so a failure here must not hide the branches
                try
                {
                    _staff = await _http.GetFromJsonAsync<List<Staff>>($"/tenant/{_companyId}/staff") ?? new();
                }
                catch
                {
                    _staff = new();
                }

                var live = _all.Where(b => !b.IsArchived).ToList();
                statTotal.Text = live.Count.ToString();
                statActive.Text = live.Count(b => b.IsActive).ToString();
                statInactive.Text = live.Count(b => !b.IsActive).ToString();

                ApplyFilter();
                if (card.Visible) RefreshTeamLabel();
            }
            catch (Exception ex)
            {
                lblInfo.Text = "Failed to load branches.";
                MessageBox.Show($"Failed to load branches: {ex.Message}");
            }
        }

        private List<Staff> TeamOf(Branch b) => _staff.Where(s => !s.IsArchived && s.BranchId == b.BranchId).ToList();

        private BranchRow ToRow(Branch b)
        {
            var team = TeamOf(b);
            var manager = team.FirstOrDefault(s => s.StaffId == b.ManagerStaffId);
            return new BranchRow
            {
                Source = b,
                BranchId = b.BranchId,
                BranchName = b.BranchName,
                Location = b.Location,
                Manager = manager?.FullName ?? "Not assigned",
                TeamSize = team.Count,
                IsActive = b.IsActive,
                IsArchived = b.IsArchived
            };
        }

        private void ApplyFilter()
        {
            var term = txtSearch.Text.Trim();
            var rows = _all.Where(b =>
                term.Length == 0 ||
                (b.BranchName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (b.Location ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
            ).Select(ToRow).ToList();

            _pager.Bind(rows);
            lblInfo.Text = $"Showing {rows.Count} branch(es). Click a row to edit it, then use Manage Team to assign people.";
        }

        // ------------------------------------------------------------ Form

        private void OpenForm(Branch? row)
        {
            if (row is null)
            {
                ClearFields();
                lblFormTitle.Text = "New Branch";
                btnSave.Text = "Add Branch";
                btnArchive.Visible = false;
                btnTeam.Visible = false;
                lblTeam.Visible = false;
            }
            else
            {
                _selectedId = row.BranchId;
                txtName.Text = row.BranchName;
                txtLocation.Text = row.Location;
                chkActive.Checked = row.IsActive;
                lblFormTitle.Text = $"Edit Branch #{row.BranchId}";
                btnSave.Text = "Save Changes";
                btnArchive.Visible = !row.IsArchived;
                btnTeam.Visible = !row.IsArchived;
                RefreshTeamLabel();
            }

            _v.Reset();
            card.Visible = true;
            txtName.Focus();
        }

        private void RefreshTeamLabel()
        {
            var b = _all.FirstOrDefault(x => x.BranchId == _selectedId);
            if (b is null || b.IsArchived) { lblTeam.Visible = false; return; }

            var team = TeamOf(b);
            var manager = team.FirstOrDefault(s => s.StaffId == b.ManagerStaffId);
            var managers = team.Count(s => s.Role == "Manager");
            var staff = team.Count(s => s.Role == "Staff");

            lblTeam.Text = $"Branch manager: {manager?.FullName ?? "not assigned"}   ·   {managers} manager(s), {staff} staff";
            lblTeam.Visible = true;
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
            txtName.Clear();
            txtLocation.Clear();
            chkActive.Checked = true;
        }

        private async Task SaveAsync()
        {
            if (!_v.ValidateAll()) return;

            var branch = new Branch
            {
                BranchName = txtName.Text.Trim(),
                Location = txtLocation.Text.Trim(),
                IsActive = chkActive.Checked
            };

            var adding = _selectedId is null;
            var response = adding
                ? await _http.PostAsJsonAsync($"/tenant/{_companyId}/branches", branch)
                : await _http.PutAsJsonAsync($"/tenant/{_companyId}/branches/{_selectedId}", branch);

            if (response.IsSuccessStatusCode)
            {
                lblInfo.Text = adding ? "Branch added. Select it to assign a team." : "Branch updated.";
                CloseForm();
                await LoadBranchesAsync();
            }
            else
            {
                lblInfo.Text = adding ? "Add failed." : "Update failed.";
                MessageBox.Show($"Failed to save branch: {response.StatusCode}\n{await response.Content.ReadAsStringAsync()}");
            }
        }

        private async Task OpenTeamAsync()
        {
            if (_selectedId is null) return;
            var branch = _all.FirstOrDefault(b => b.BranchId == _selectedId);
            if (branch is null) return;

            using (var dialog = new BranchTeamForm(_http, _companyId, branch.BranchId, branch.BranchName))
                dialog.ShowDialog(FindForm());

            // The dialog saves as it goes, so refresh the counts and manager whichever way it was closed
            await LoadBranchesAsync();
        }

        private async Task ArchiveAsync()
        {
            if (_selectedId is null) return;
            if (MessageBox.Show("Archive this branch?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            var response = await _http.PutAsync($"/tenant/{_companyId}/branches/{_selectedId}/archive", null);
            if (response.IsSuccessStatusCode)
            {
                lblInfo.Text = "Branch archived.";
                CloseForm();
                await LoadBranchesAsync();
            }
            else
            {
                // e.g. 409 when people are still assigned: show the server's explanation
                var msg = (await response.Content.ReadAsStringAsync()).Trim().Trim('"');
                lblInfo.Text = "Archive failed.";
                MessageBox.Show(string.IsNullOrWhiteSpace(msg) ? $"Failed to archive branch: {response.StatusCode}" : msg,
                    "Cannot archive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}