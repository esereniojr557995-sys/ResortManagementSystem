using System.Drawing;
using System.Net.Http.Json;
using ResortManagementSystem.Domain.Entities;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    /// <summary>Choose which Managers and Staff work at one branch, and who manages it.</summary>
    public class BranchTeamForm : Form
    {
        private readonly HttpClient _http;
        private readonly int _companyId;
        private readonly int _branchId;
        private readonly string _branchName;

        private List<Staff> _staff = new();
        private List<Branch> _branches = new();

        // One grid row (used by both grids)
        private class PersonRow
        {
            public int StaffId { get; set; }
            public string FullName { get; set; } = "";
            public string Role { get; set; } = "";
            public string Detail { get; set; } = "";     // team grid: title; available grid: where they work now
            public string Status { get; set; } = "";
            public bool IsActive { get; set; }
            public bool IsManagerHere { get; set; }
            public bool IsElsewhere { get; set; }
        }

        private DataGridView dgvTeam = null!, dgvAvailable = null!;
        private TextBox txtFind = null!;
        private Label lblManager = null!, lblStatus = null!;
        private Button btnAssign = null!, btnUnassign = null!, btnSetManager = null!, btnClearManager = null!;

        public BranchTeamForm(HttpClient http, int companyId, int branchId, string branchName)
        {
            _http = http;
            _companyId = companyId;
            _branchId = branchId;
            _branchName = branchName;

            Text = $"Branch team: {branchName}";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1060, 640);
            MinimumSize = new Size(940, 560);
            BackColor = ContentBg;
            Font = new Font("Segoe UI", 9.5f);
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildUi(branchName);

            Shown += async (s, e) => await ReloadAsync();
        }

        private void BuildUi(string branchName)
        {
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 8), BackColor = ContentBg };

            var title = new Label
            {
                Text = branchName,
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = TextDark,
                Dock = DockStyle.Top,
                Height = 38,
                AutoEllipsis = true,
                UseMnemonic = false
            };
            var sub = new Label
            {
                Text = "Assign Managers and Staff to this branch and choose who manages it. Admin accounts oversee every branch, so they are not listed.",
                ForeColor = TextMuted,
                Dock = DockStyle.Top,
                Height = 28,
                UseMnemonic = false
            };

            // ---- Layout: assigned team | action buttons | available staff ----
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4, BackColor = ContentBg };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));    // headings
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));    // manager line | search box
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // grids
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));    // buttons under the team grid

            Label Heading(string text) => new()
            {
                Text = text,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = TextDark,
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            table.Controls.Add(Heading("Assigned to this branch"), 0, 0);
            table.Controls.Add(Heading("Available staff"), 2, 0);

            lblManager = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = AccentDark,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseMnemonic = false
            };
            table.Controls.Add(lblManager, 0, 1);

            txtFind = new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 5, 0, 0),
                PlaceholderText = "Search by name, role or branch",
                BorderStyle = BorderStyle.FixedSingle
            };
            table.Controls.Add(txtFind, 2, 1);

            // ---- Grids ----
            dgvTeam = new DataGridView { Dock = DockStyle.Fill, MultiSelect = false };
            StyleGrid(dgvTeam);
            AddTextColumn(dgvTeam, nameof(PersonRow.FullName), "Name", 170);
            AddTextColumn(dgvTeam, nameof(PersonRow.Role), "Role", 85);
            AddTextColumn(dgvTeam, nameof(PersonRow.Detail), "Title", 130);
            AddTextColumn(dgvTeam, nameof(PersonRow.Status), "Status", 80);
            MakeBadgeColumn(dgvTeam, nameof(PersonRow.Role), role => role == "Manager" ? BadgeGreen : BadgeGray);
            MakeBadgeColumn(dgvTeam, nameof(PersonRow.Status), st => st == "Active" ? BadgeGreen : BadgeGray);
            table.Controls.Add(dgvTeam, 0, 2);

            dgvAvailable = new DataGridView { Dock = DockStyle.Fill, MultiSelect = false };
            StyleGrid(dgvAvailable);
            AddTextColumn(dgvAvailable, nameof(PersonRow.FullName), "Name", 170);
            AddTextColumn(dgvAvailable, nameof(PersonRow.Role), "Role", 85);
            AddTextColumn(dgvAvailable, nameof(PersonRow.Detail), "Currently at", 140);
            MakeBadgeColumn(dgvAvailable, nameof(PersonRow.Role), role => role == "Manager" ? BadgeGreen : BadgeGray);
            table.Controls.Add(dgvAvailable, 2, 2);

            // ---- Middle buttons (kept vertically centred) ----
            var mid = new Panel { Dock = DockStyle.Fill, BackColor = ContentBg };
            btnAssign = MakePillButton("◄  Assign", Accent, Color.White, 0, 0, 138);
            btnUnassign = MakePillButton("Unassign  ►", CardBg, Danger, 0, 0, 138);
            mid.Controls.Add(btnAssign);
            mid.Controls.Add(btnUnassign);
            void LayoutMid()
            {
                var top = Math.Max(0, mid.Height / 2 - 48);
                btnAssign.SetBounds(9, top, 138, 40);
                btnUnassign.SetBounds(9, top + 52, 138, 40);
            }
            mid.Resize += (s, e) => LayoutMid();
            LayoutMid();
            table.Controls.Add(mid, 1, 2);

            // ---- Manager buttons under the team grid ----
            var teamButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = ContentBg, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
            btnSetManager = MakePillButton("Set as branch manager", Accent, Color.White, 0, 0, 210);
            btnClearManager = MakePillButton("Remove manager", CardBg, Danger, 0, 0, 160);
            btnClearManager.Margin = new Padding(10, 3, 3, 3);
            teamButtons.Controls.Add(btnSetManager);
            teamButtons.Controls.Add(btnClearManager);
            table.Controls.Add(teamButtons, 0, 3);

            var hint = new Label
            {
                Text = "Only active Managers and Staff are listed. Double-click a name to assign it.",
                Dock = DockStyle.Fill,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            table.Controls.Add(hint, 2, 3);

            // ---- Footer ----
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = ContentBg };
            var closeHost = new Panel { Dock = DockStyle.Right, Width = 130, BackColor = ContentBg };
            var btnClose = MakePillButton("Close", TextDark, Color.White, 10, 9, 110);
            btnClose.DialogResult = DialogResult.OK;
            closeHost.Controls.Add(btnClose);
            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            footer.Controls.Add(lblStatus);
            footer.Controls.Add(closeHost);
            AcceptButton = btnClose;
            CancelButton = btnClose;

            root.Controls.Add(table);      // Fill first, then the Top-docked bits (last added = top-most)
            root.Controls.Add(sub);
            root.Controls.Add(title);
            Controls.Add(root);
            Controls.Add(footer);

            // Events last, so every control exists when they first fire
            dgvTeam.SelectionChanged += (s, e) => UpdateButtons();
            dgvAvailable.SelectionChanged += (s, e) => UpdateButtons();
            dgvAvailable.CellDoubleClick += async (s, e) => { if (e.RowIndex >= 0) await AssignAsync(); };
            txtFind.TextChanged += (s, e) => BindGrids();
            btnAssign.Click += async (s, e) => await AssignAsync();
            btnUnassign.Click += async (s, e) => await UnassignAsync();
            btnSetManager.Click += async (s, e) => await SetManagerAsync();
            btnClearManager.Click += async (s, e) => await ClearManagerAsync();

            UpdateButtons();
        }

        // ------------------------------------------------------------ Data

        private int? ManagerId => _branches.FirstOrDefault(b => b.BranchId == _branchId)?.ManagerStaffId;

        private async Task ReloadAsync(string? message = null)
        {
            try
            {
                lblStatus.Text = "Loading…";
                _staff = await _http.GetFromJsonAsync<List<Staff>>($"/tenant/{_companyId}/staff") ?? new();
                _branches = await _http.GetFromJsonAsync<List<Branch>>($"/tenant/{_companyId}/branches") ?? new();
                BindGrids();
                lblStatus.Text = message ?? $"{dgvTeam.Rows.Count} person(s) on this team.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Could not load the team.";
                MessageBox.Show(this, $"Could not load the team: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BindGrids()
        {
            var managerId = ManagerId;
            var branchNames = _branches.ToDictionary(b => b.BranchId, b => b.BranchName);

            var team = _staff
                .Where(s => !s.IsArchived && s.BranchId == _branchId)
                .OrderBy(s => s.StaffId == managerId ? 0 : 1)
                .ThenBy(s => s.Role == "Manager" ? 0 : 1)
                .ThenBy(s => s.FullName)
                .Select(s => new PersonRow
                {
                    StaffId = s.StaffId,
                    FullName = s.FullName,
                    Role = s.Role,
                    Detail = s.StaffId == managerId ? "Branch manager" : "",
                    Status = s.IsActive ? "Active" : "Inactive",
                    IsActive = s.IsActive,
                    IsManagerHere = s.StaffId == managerId
                }).ToList();
            dgvTeam.DataSource = team;

            var manager = managerId is null ? null : team.FirstOrDefault(p => p.StaffId == managerId);
            lblManager.Text = manager is null ? "Branch manager: not chosen yet" : $"Branch manager: {manager.FullName}";

            var term = txtFind.Text.Trim();
            var available = _staff
                .Where(s => !s.IsArchived && s.IsActive && (s.Role is "Manager" or "Staff") && s.BranchId != _branchId)
                .Select(s =>
                {
                    string? where = s.BranchId is int b && branchNames.TryGetValue(b, out var n) ? n : null;
                    return new PersonRow
                    {
                        StaffId = s.StaffId,
                        FullName = s.FullName,
                        Role = s.Role,
                        Detail = where ?? "Unassigned",
                        IsActive = s.IsActive,
                        IsElsewhere = where is not null
                    };
                })
                .Where(p => term.Length == 0 ||
                            p.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            p.Role.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            p.Detail.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.IsElsewhere ? 1 : 0)     // people with no branch first
                .ThenBy(p => p.FullName)
                .ToList();
            dgvAvailable.DataSource = available;

            UpdateButtons();
        }

        private static PersonRow? Current(DataGridView grid) => grid.CurrentRow?.DataBoundItem as PersonRow;

        private void UpdateButtons()
        {
            if (btnClearManager is null) return;   // a grid fired SelectionChanged while the form was still being built

            var t = Current(dgvTeam);
            var a = Current(dgvAvailable);
            btnAssign.Enabled = a is not null;
            btnUnassign.Enabled = t is not null;
            btnSetManager.Enabled = t is not null && t.Role == "Manager" && t.IsActive && !t.IsManagerHere;
            btnClearManager.Enabled = ManagerId is not null;
        }

        // ------------------------------------------------------------ Actions

        private async Task<bool> PutAsync(string url, object body, string failTitle)
        {
            try
            {
                var response = await _http.PutAsJsonAsync(url, body);
                if (response.IsSuccessStatusCode) return true;

                var msg = (await response.Content.ReadAsStringAsync()).Trim().Trim('"');
                MessageBox.Show(this, string.IsNullOrWhiteSpace(msg) ? $"{failTitle} ({(int)response.StatusCode})." : msg,
                    failTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not reach the server: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private async Task AssignAsync()
        {
            var a = Current(dgvAvailable);
            if (a is null) return;

            if (a.IsElsewhere)
            {
                var ok = MessageBox.Show(this, $"{a.FullName} currently works at {a.Detail}.\n\nMove them to {_branchName}?",
                    "Move to this branch", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ok != DialogResult.Yes) return;
            }

            if (await PutAsync($"/tenant/{_companyId}/staff/{a.StaffId}/branch", new { branchId = _branchId }, "Could not assign"))
                await ReloadAsync($"{a.FullName} assigned to {_branchName}.");
        }

        private async Task UnassignAsync()
        {
            var t = Current(dgvTeam);
            if (t is null) return;

            var extra = t.IsManagerHere
                ? "\n\nThey are the branch manager, so the branch will have no manager until you choose another."
                : "";
            var ok = MessageBox.Show(this, $"Remove {t.FullName} from {_branchName}?{extra}",
                "Unassign", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ok != DialogResult.Yes) return;

            if (await PutAsync($"/tenant/{_companyId}/staff/{t.StaffId}/branch", new { branchId = (int?)null }, "Could not unassign"))
                await ReloadAsync($"{t.FullName} removed from {_branchName}.");
        }

        private async Task SetManagerAsync()
        {
            var t = Current(dgvTeam);
            if (t is null) return;

            if (await PutAsync($"/tenant/{_companyId}/branches/{_branchId}/manager", new { staffId = t.StaffId }, "Could not set the manager"))
                await ReloadAsync($"{t.FullName} is now the manager of {_branchName}.");
        }

        private async Task ClearManagerAsync()
        {
            if (ManagerId is null) return;

            if (await PutAsync($"/tenant/{_companyId}/branches/{_branchId}/manager", new { staffId = (int?)null }, "Could not remove the manager"))
                await ReloadAsync("Branch manager removed.");
        }
    }
}