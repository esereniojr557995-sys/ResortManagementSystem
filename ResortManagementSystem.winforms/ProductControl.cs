using System.Net.Http.Json;
using System.Drawing;
using ResortManagementSystem.Domain.Entities;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class ProductsControl : UserControl
    {
        private readonly HttpClient _http;
        private readonly int _companyId;

        private DataGridView dgvProducts = null!;
        private Pager _pager = null!;
        private TextBox txtCode = null!, txtName = null!, txtPrice = null!;
        private CheckBox chkActive = null!;
        private TextBox txtSearch = null!;
        private ComboBox cboStatusFilter = null!;
        private CheckBox chkShowArchived = null!;
        private Label lblStatus = null!, lblFormTitle = null!, lblFormError = null!;
        private Panel card = null!;
        private Button btnSave = null!, btnCancel = null!, btnArchive = null!, btnRestore = null!;
        private Label statTotal = null!, statActive = null!, statAvgPrice = null!, statArchived = null!;
        private FormValidator _v = null!;

        private int? _selectedId = null;
        private List<Product> _all = new();

        public ProductsControl(HttpClient http, int companyId)
        {
            _http = http;
            _companyId = companyId;
            Dock = DockStyle.Fill;
            BackColor = ContentBg;
            Padding = new Padding(36, 28, 36, 28);
            BuildUi();
        }

        public async Task LoadAsync() => await LoadProductsAsync();

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = ContentBg };
            var topBar = BuildTopBar("Rooms & Services", "Manage bookable rooms and resort services", out _);

            var statsRow = MakeStatsRow(
                ("Total Listings", Accent, "rooms and services"),
                ("Active", Accent, "available to book"),
                ("Avg. Price", AccentAmber, "per listing"),
                ("Archived", Danger, "hidden from booking")
            );
            statTotal = (Label)statsRow.Controls[0].Tag;
            statActive = (Label)statsRow.Controls[1].Tag;
            statAvgPrice = (Label)statsRow.Controls[2].Tag;
            statArchived = (Label)statsRow.Controls[3].Tag;

            // ---- Toolbar ----
            var toolbar = MakeToolbar("+ New Listing", out var btnNew, out var btnRefresh);
            txtSearch = MakeSearchBox(56, 240);
            cboStatusFilter = MakeFilterCombo(378, 130, "All", "Active", "Inactive");
            chkShowArchived = new CheckBox { Text = "Show archived", Left = 526, Top = 16, AutoSize = true };
            toolbar.Controls.AddRange(new Control[]
            {
                MakeToolbarLabel("Search", 0), txtSearch,
                MakeToolbarLabel("Status", 312), cboStatusFilter,
                chkShowArchived
            });

            // ---- Form card ----
            card = MakeFormCard(218, out lblFormTitle);
            int col1 = 24, col2 = col1 + LabelWidth + FieldWidth + ColGap;

            var lblCode = MakeFieldLabel("Code", col1, 58);
            txtCode = MakeTextBox(col1 + LabelWidth, 58);
            var lblName = MakeFieldLabel("Name", col1, 96);
            txtName = MakeTextBox(col1 + LabelWidth, 96);
            var lblPrice = MakeFieldLabel("Unit Price", col2, 58);
            txtPrice = MakeTextBox(col2 + LabelWidth, 58);
            txtPrice.PlaceholderText = "e.g. 2500.00";
            chkActive = new CheckBox { Text = "Active (available to book)", Left = col2 + LabelWidth, Top = 96, AutoSize = true, Checked = true };

            lblFormError = new Label { Left = col1, Top = 130, Width = 900, Height = 22, ForeColor = Danger, Font = new Font("Segoe UI", 9f, FontStyle.Bold), UseMnemonic = false };

            int btnTop = 160;
            btnSave = MakePillButton("Add Listing", Accent, Color.White, col1, btnTop, 150);
            btnCancel = MakePillButton("Cancel", CardBg, TextMuted, col1 + 162, btnTop, 110);
            btnArchive = MakePillButton("Archive", Danger, Color.White, col1 + 286, btnTop, 130);
            btnRestore = MakePillButton("Restore", TextDark, Color.White, col1 + 286, btnTop, 130);
            btnArchive.Visible = false;
            btnRestore.Visible = false;

            card.Controls.AddRange(new Control[]
            {
                lblCode, txtCode, lblName, txtName, lblPrice, txtPrice, chkActive,
                lblFormError, btnSave, btnCancel, btnArchive, btnRestore
            });

            // ---- Validation ----
            _v = new FormValidator(lblFormError);
            _v.Watch(txtCode, () => InputRules.Code(txtCode.Text, "Code", 2, 20) ?? DuplicateCodeMessage());
            _v.Watch(txtName, () => InputRules.Place(txtName.Text, "Name", 2, 100, required: true));
            _v.Watch(txtPrice, () => InputRules.Number(txtPrice.Text, "Unit price", 0m, 1_000_000m));

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
            dgvProducts = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvProducts);
            AddTextColumn(dgvProducts, nameof(Product.ProductId), "ID", 40);
            AddTextColumn(dgvProducts, nameof(Product.ProductCode), "Code", 90);
            AddTextColumn(dgvProducts, nameof(Product.ProductName), "Name", 220);
            AddTextColumn(dgvProducts, nameof(Product.UnitPrice), "Unit Price", 110);
            AddTextColumn(dgvProducts, nameof(Product.IsActive), "Active", 70);
            AddTextColumn(dgvProducts, nameof(Product.IsArchived), "Archived", 70);
            dgvProducts.Columns[nameof(Product.UnitPrice)].DefaultCellStyle.Format = "₱#,##0.00";
            MakeYesNoColumn(dgvProducts, nameof(Product.IsActive), BadgeGreen, BadgeGray);
            MakeYesNoColumn(dgvProducts, nameof(Product.IsArchived), BadgeAmber, BadgeGray);

            // ---- Events ----
            btnNew.Click += (s, e) => OpenForm(null);
            btnRefresh.Click += async (s, e) => await LoadProductsAsync();
            btnSave.Click += async (s, e) => await SaveAsync();
            btnCancel.Click += (s, e) => CloseForm();
            btnArchive.Click += async (s, e) => await ArchiveAsync();
            btnRestore.Click += async (s, e) => await RestoreAsync();
            txtSearch.TextChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };
            cboStatusFilter.SelectedIndexChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };
            chkShowArchived.CheckedChanged += async (s, e) => await LoadProductsAsync();
            dgvProducts.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvProducts.Rows[e.RowIndex].DataBoundItem is Product p)
                    OpenForm(p);
            };

            _pager = new Pager(dgvProducts);
            Controls.Add(dgvProducts);
            Controls.Add(_pager);
            Controls.Add(lblStatus);
            Controls.Add(header);
        }

        private string? DuplicateCodeMessage()
        {
            var code = txtCode.Text.Trim();
            return _all.Any(p => p.ProductId != _selectedId && p.ProductCode.Equals(code, StringComparison.OrdinalIgnoreCase))
                ? "That code is already used by another listing."
                : null;
        }

        // ------------------------------------------------------------ Data

        private async Task LoadProductsAsync()
        {
            try
            {
                lblStatus.Text = "Loading rooms & services…";
                var url = $"/tenant/{_companyId}/products?includeArchived={chkShowArchived.Checked}";
                _all = await _http.GetFromJsonAsync<List<Product>>(url) ?? new();

                var live = _all.Where(p => !p.IsArchived).ToList();
                statTotal.Text = live.Count.ToString();
                statActive.Text = live.Count(p => p.IsActive).ToString();
                statAvgPrice.Text = live.Count > 0 ? $"₱{live.Average(p => p.UnitPrice):N0}" : "₱0";
                statArchived.Text = _all.Count(p => p.IsArchived).ToString();

                ApplyFilter();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Failed to load rooms & services.";
                MessageBox.Show($"Failed to load rooms & services: {ex.Message}");
            }
        }

        private void ApplyFilter()
        {
            var term = txtSearch.Text.Trim();
            var status = cboStatusFilter.SelectedItem?.ToString() ?? "All";

            var filtered = _all.Where(p =>
                (status == "All" || (status == "Active" && p.IsActive) || (status == "Inactive" && !p.IsActive)) &&
                (term.Length == 0 ||
                 p.ProductCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 p.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            _pager.Bind(filtered);
            lblStatus.Text = $"Showing {filtered.Count} listing(s). Click a row to edit it.";
        }

        // ------------------------------------------------------------ Form

        private void OpenForm(Product? p)
        {
            if (p is null)
            {
                ClearFields();
                lblFormTitle.Text = "New Room / Service";
                btnSave.Text = "Add Listing";
                btnArchive.Visible = false;
                btnRestore.Visible = false;
            }
            else
            {
                _selectedId = p.ProductId;
                txtCode.Text = p.ProductCode;
                txtName.Text = p.ProductName;
                txtPrice.Text = p.UnitPrice.ToString("0.00");
                chkActive.Checked = p.IsActive;
                lblFormTitle.Text = $"Edit Listing #{p.ProductId}";
                btnSave.Text = "Save Changes";
                btnArchive.Visible = !p.IsArchived;
                btnRestore.Visible = p.IsArchived;
            }

            _v.Reset();
            card.Visible = true;
            txtCode.Focus();
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
            txtName.Clear();
            txtPrice.Clear();
            chkActive.Checked = true;
        }

        private async Task SaveAsync()
        {
            if (!_v.ValidateAll()) return;

            var product = new Product
            {
                ProductCode = txtCode.Text.Trim(),
                ProductName = txtName.Text.Trim(),
                UnitPrice = decimal.Parse(txtPrice.Text.Trim(), System.Globalization.CultureInfo.InvariantCulture),
                IsActive = chkActive.Checked
            };

            var adding = _selectedId is null;
            try
            {
                var response = adding
                    ? await _http.PostAsJsonAsync($"/tenant/{_companyId}/products", product)
                    : await _http.PutAsJsonAsync($"/tenant/{_companyId}/products/{_selectedId}", product);

                if (response.IsSuccessStatusCode)
                {
                    lblStatus.Text = adding ? "Listing added." : "Listing updated.";
                    CloseForm();
                    await LoadProductsAsync();
                }
                else
                {
                    lblStatus.Text = adding ? "Add failed." : "Update failed.";
                    MessageBox.Show($"Failed to save listing: {response.StatusCode}\n{await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Save failed.";
                MessageBox.Show($"Could not reach the server: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ArchiveAsync()
        {
            if (_selectedId is null) return;
            if (MessageBox.Show("Archive this listing? It will be hidden from the booking list but existing reservations are preserved.", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            await PutAndReloadAsync($"/tenant/{_companyId}/products/{_selectedId}/archive", "Listing archived.", "Archive failed.");
        }

        private async Task RestoreAsync()
        {
            if (_selectedId is null) return;
            await PutAndReloadAsync($"/tenant/{_companyId}/products/{_selectedId}/unarchive", "Listing restored.", "Restore failed.");
        }

        private async Task PutAndReloadAsync(string url, string okText, string failText)
        {
            try
            {
                var response = await _http.PutAsync(url, null);
                if (response.IsSuccessStatusCode)
                {
                    lblStatus.Text = okText;
                    CloseForm();
                    await LoadProductsAsync();
                }
                else
                {
                    lblStatus.Text = failText;
                    MessageBox.Show($"{failText} ({response.StatusCode})");
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = failText;
                MessageBox.Show($"Could not reach the server: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}