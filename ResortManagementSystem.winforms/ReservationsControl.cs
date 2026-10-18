using System.Globalization;
using System.Net.Http.Json;
using System.Drawing;
using ResortManagementSystem.Domain.Entities;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class ReservationsControl : UserControl
    {
        private readonly HttpClient _http;
        private readonly int _companyId;

        private DataGridView dgvReservations = null!;

        private Pager _pager = null!;
        private ComboBox cboCustomer = null!, cboProduct = null!, cboStatus = null!, cboPromotion = null!;
        private DateTimePicker dtpCheckIn = null!, dtpCheckOut = null!;
        private TextBox txtTotalAmount = null!;
        private Label lblBreakdown = null!, lblInfo = null!, lblFormTitle = null!, lblFormError = null!;
        private Panel card = null!;
        private Button btnSave = null!, btnCancel = null!, btnArchive = null!;
        private TextBox txtSearch = null!;
        private ComboBox cboStatusFilter = null!;
        private DateTimePicker dtpFrom = null!, dtpTo = null!;
        private CheckBox chkShowArchived = null!;
        private Label statTotal = null!, statPending = null!, statConfirmed = null!, statLost = null!;
        private FormValidator _v = null!;

        private int? _selectedReservationId = null;
        private bool _suppressRecalc = false;

        private List<Customer> _allCustomersFull = new();
        private List<Promotion> _allPromotions = new();
        private List<ReservationRow> _all = new();

        private class LookupItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public decimal UnitPrice { get; set; }
            public override string ToString() => Name;
        }

        private class PromoDisplayItem
        {
            public Promotion Promotion { get; set; } = null!;
            public override string ToString() => $"{Promotion.Code} — {Promotion.DiscountPercent:0.##}% off";
        }

        private class ReservationRow
        {
            public int ReservationId { get; set; }
            public int CustomerId { get; set; }
            public int ProductId { get; set; }
            public string CustomerName { get; set; } = "";
            public string ProductName { get; set; } = "";
            public DateTime CheckInDate { get; set; }
            public DateTime CheckOutDate { get; set; }
            public int Nights { get; set; }
            public string Status { get; set; } = "";
            public decimal TotalAmount { get; set; }
            public bool IsArchived { get; set; }
        }

        public ReservationsControl(HttpClient http, int companyId)
        {
            _http = http;
            _companyId = companyId;
            Dock = DockStyle.Fill;
            BackColor = ContentBg;
            Padding = new Padding(36, 28, 36, 28);
            BuildUi();
        }

        public async Task LoadAsync()
        {
            await LoadCustomerLookupAsync();
            await LoadProductLookupAsync();
            await LoadPromotionLookupAsync();
            await LoadReservationsAsync();
        }

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = ContentBg };

            var topBar = BuildTopBar("Reservations", "Bookings across rooms and services", out _);

            var statsRow = MakeStatsRow(
                ("Total Reservations", Accent, "all bookings on record"),
                ("Pending", AccentAmber, "awaiting confirmation"),
                ("Confirmed", Accent, "incl. checked in / out"),
                ("Cancelled / Refunded", Danger, "lost bookings")
            );
            statTotal = (Label)statsRow.Controls[0].Tag;
            statPending = (Label)statsRow.Controls[1].Tag;
            statConfirmed = (Label)statsRow.Controls[2].Tag;
            statLost = (Label)statsRow.Controls[3].Tag;

            // ---- Toolbar ----
            var toolbar = MakeToolbar("+ New Reservation", out var btnNew, out var btnRefresh);
            txtSearch = MakeSearchBox(56, 210);
            cboStatusFilter = MakeFilterCombo(330, 130, "All statuses", "Pending", "Confirmed", "CheckedIn", "CheckedOut", "Cancelled", "Refunded");
            dtpFrom = new DateTimePicker { Left = 566, Top = 13, Width = 120, Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
            dtpTo = new DateTimePicker { Left = 716, Top = 13, Width = 120, Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
            chkShowArchived = new CheckBox { Text = "Show archived", Left = 854, Top = 16, AutoSize = true };
            toolbar.Controls.AddRange(new Control[]
            {
                MakeToolbarLabel("Search", 0), txtSearch,
                MakeToolbarLabel("Status", 282), cboStatusFilter,
                MakeToolbarLabel("Check-in from", 470), dtpFrom,
                MakeToolbarLabel("to", 696), dtpTo,
                chkShowArchived
            });

            // ---- Form card ----
            card = MakeFormCard(290, out lblFormTitle);

            int col1 = 24, col2 = col1 + LabelWidth + FieldWidth + ColGap;

            var lblCustomer = MakeFieldLabel("Customer", col1, 58);
            cboCustomer = MakeComboBox(col1 + LabelWidth, 58);
            var lblProduct = MakeFieldLabel("Room/Service", col2, 58);
            cboProduct = MakeComboBox(col2 + LabelWidth, 58);

            var lblCheckIn = MakeFieldLabel("Check-In", col1, 96);
            dtpCheckIn = new DateTimePicker { Left = col1 + LabelWidth, Top = 93, Width = FieldWidth, Format = DateTimePickerFormat.Short };
            var lblCheckOut = MakeFieldLabel("Check-Out", col2, 96);
            dtpCheckOut = new DateTimePicker { Left = col2 + LabelWidth, Top = 93, Width = FieldWidth, Format = DateTimePickerFormat.Short };

            var lblStatusField = MakeFieldLabel("Status", col1, 134);
            cboStatus = MakeComboBox(col1 + LabelWidth, 134);
            cboStatus.Items.AddRange(new object[] { "Pending", "Confirmed", "CheckedIn", "CheckedOut", "Cancelled", "Refunded" });
            cboStatus.SelectedIndex = 0;

            var lblAmount = MakeFieldLabel("Total Amount", col2, 134);
            txtTotalAmount = MakeTextBox(col2 + LabelWidth, 134);
            txtTotalAmount.ReadOnly = true;     // calculated from room price, nights and promotion
            txtTotalAmount.BackColor = ContentBg;

            lblBreakdown = new Label { Left = col2 + LabelWidth, Top = 164, Width = 520, Height = 20, ForeColor = TextMuted, Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), UseMnemonic = false };

            var lblPromo = MakeFieldLabel("Promotion", col1, 172);
            cboPromotion = MakeComboBox(col1 + LabelWidth, 172);

            lblFormError = new Label { Left = col1, Top = 206, Width = 900, Height = 22, ForeColor = Danger, Font = new Font("Segoe UI", 9f, FontStyle.Bold), UseMnemonic = false };

            int btnTop = 234;
            btnSave = MakePillButton("Add Reservation", Accent, Color.White, col1, btnTop, 160);
            btnCancel = MakePillButton("Cancel", CardBg, TextMuted, col1 + 172, btnTop, 110);
            btnArchive = MakePillButton("Archive", Danger, Color.White, col1 + 296, btnTop, 130);
            btnArchive.Visible = false;

            card.Controls.AddRange(new Control[]
            {
                lblCustomer, cboCustomer, lblProduct, cboProduct,
                lblCheckIn, dtpCheckIn, lblCheckOut, dtpCheckOut,
                lblStatusField, cboStatus, lblAmount, txtTotalAmount, lblBreakdown,
                lblPromo, cboPromotion, lblFormError,
                btnSave, btnCancel, btnArchive
            });

            // ---- Validation rules ----
            _v = new FormValidator(lblFormError);
            _v.Watch(cboCustomer, () => cboCustomer.SelectedItem is null ? "Select a customer." : null);
            _v.Watch(cboProduct, () => cboProduct.SelectedItem is null ? "Select a room or service." : null);
            _v.Watch(dtpCheckIn, () =>
                _selectedReservationId is null && dtpCheckIn.Value.Date < DateTime.Today
                    ? "Check-in date cannot be in the past for a new reservation."
                    : null);
            _v.Watch(dtpCheckOut, () =>
            {
                var nights = (dtpCheckOut.Value.Date - dtpCheckIn.Value.Date).Days;
                if (nights < 1) return "Check-out must be at least one day after check-in.";
                if (nights > 60) return "A single stay cannot be longer than 60 nights.";
                return null;
            });
            _v.Watch(cboStatus, () => cboStatus.SelectedItem is null ? "Select a status." : null);
            _v.Watch(txtTotalAmount, () => InputRules.Number(txtTotalAmount.Text, "Total amount", 0.01m, 10_000_000m));

            // Recalculate the total whenever something that affects the price changes
            cboProduct.SelectedIndexChanged += (s, e) => RecalculateTotal();
            cboPromotion.SelectedIndexChanged += (s, e) => RecalculateTotal();
            dtpCheckIn.ValueChanged += (s, e) => { RecalculateTotal(); _v.Recheck(dtpCheckOut); };
            dtpCheckOut.ValueChanged += (s, e) => RecalculateTotal();
            cboCustomer.SelectedIndexChanged += (s, e) => { RefreshEligiblePromotions(); RecalculateTotal(); };

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
            dgvReservations = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(dgvReservations);
            AddTextColumn(dgvReservations, nameof(ReservationRow.ReservationId), "ID", 40);
            AddTextColumn(dgvReservations, nameof(ReservationRow.CustomerName), "Customer", 150);
            AddTextColumn(dgvReservations, nameof(ReservationRow.ProductName), "Room/Service", 150);
            AddTextColumn(dgvReservations, nameof(ReservationRow.CheckInDate), "Check-In", 95);
            AddTextColumn(dgvReservations, nameof(ReservationRow.CheckOutDate), "Check-Out", 95);
            AddTextColumn(dgvReservations, nameof(ReservationRow.Nights), "Nights", 60);
            AddTextColumn(dgvReservations, nameof(ReservationRow.Status), "Status", 100);
            AddTextColumn(dgvReservations, nameof(ReservationRow.TotalAmount), "Total Amount", 110);
            AddTextColumn(dgvReservations, nameof(ReservationRow.IsArchived), "Archived", 70);
            dgvReservations.Columns[nameof(ReservationRow.CheckInDate)].DefaultCellStyle.Format = "MM/dd/yyyy";
            dgvReservations.Columns[nameof(ReservationRow.CheckOutDate)].DefaultCellStyle.Format = "MM/dd/yyyy";
            dgvReservations.Columns[nameof(ReservationRow.TotalAmount)].DefaultCellStyle.Format = "₱#,##0.00";
            MakeBadgeColumn(dgvReservations, nameof(ReservationRow.Status), status => status switch
            {
                "Confirmed" or "CheckedIn" => BadgeGreen,
                "Pending" => BadgeAmber,
                "Cancelled" or "Refunded" => BadgeRed,
                "CheckedOut" => BadgeBlue,
                _ => BadgeGray
            });
            MakeYesNoColumn(dgvReservations, nameof(ReservationRow.IsArchived), BadgeAmber, BadgeGray);

            // ---- Events ----
            btnNew.Click += (s, e) => OpenForm(null);
            btnRefresh.Click += async (s, e) => await LoadAsync();
            btnSave.Click += async (s, e) => await SaveAsync();
            btnCancel.Click += (s, e) => CloseForm();
            btnArchive.Click += async (s, e) => await ArchiveAsync();
            txtSearch.TextChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };
            cboStatusFilter.SelectedIndexChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };
            dtpFrom.ValueChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };
            dtpTo.ValueChanged += (s, e) => { _pager.ResetPage(); ApplyFilter(); };
            chkShowArchived.CheckedChanged += async (s, e) => await LoadReservationsAsync();
            dgvReservations.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvReservations.Rows[e.RowIndex].DataBoundItem is ReservationRow row)
                    OpenForm(row);
            };

            _pager = new Pager(dgvReservations);

            Controls.Add(dgvReservations);

            Controls.Add(_pager);
            Controls.Add(lblInfo);
            Controls.Add(header);
        }

        // ------------------------------------------------------------ Lookups

        private async Task LoadCustomerLookupAsync()
        {
            try
            {
                var customers = await _http.GetFromJsonAsync<List<Customer>>($"/tenant/{_companyId}/customers");
                _allCustomersFull = customers ?? new();
                cboCustomer.Items.Clear();
                foreach (var c in _allCustomersFull)
                    cboCustomer.Items.Add(new LookupItem { Id = c.CustomerId, Name = $"#{c.CustomerId} — {c.FullName}" });
                if (cboCustomer.Items.Count > 0) cboCustomer.SelectedIndex = 0;
            }
            catch (Exception ex) { MessageBox.Show($"Failed to load customers for dropdown: {ex.Message}"); }
        }

        private async Task LoadProductLookupAsync()
        {
            try
            {
                var products = await _http.GetFromJsonAsync<List<Product>>($"/tenant/{_companyId}/products");
                cboProduct.Items.Clear();
                foreach (var p in products ?? new())
                    cboProduct.Items.Add(new LookupItem { Id = p.ProductId, Name = $"#{p.ProductId} — {p.ProductName}", UnitPrice = p.UnitPrice });
                if (cboProduct.Items.Count > 0) cboProduct.SelectedIndex = 0;
            }
            catch (Exception ex) { MessageBox.Show($"Failed to load products for dropdown: {ex.Message}"); }
        }

        private async Task LoadPromotionLookupAsync()
        {
            try
            {
                _allPromotions = await _http.GetFromJsonAsync<List<Promotion>>($"/tenant/{_companyId}/promotions") ?? new();
                RefreshEligiblePromotions();
            }
            catch (Exception ex) { MessageBox.Show($"Failed to load promotions: {ex.Message}"); }
        }

        private void RefreshEligiblePromotions()
        {
            cboPromotion.Items.Clear();
            cboPromotion.Items.Add("None");

            if (cboCustomer.SelectedItem is LookupItem selectedCustomer)
            {
                var customer = _allCustomersFull.FirstOrDefault(c => c.CustomerId == selectedCustomer.Id);
                if (customer != null)
                {
                    var category = string.IsNullOrWhiteSpace(customer.CustomerCategory) ? "Regular" : customer.CustomerCategory;
                    var eligible = _allPromotions.Where(p =>
                        p.IsActive && (string.IsNullOrWhiteSpace(p.EligibleCategory) || p.EligibleCategory == "All" || p.EligibleCategory == category));
                    foreach (var promo in eligible)
                        cboPromotion.Items.Add(new PromoDisplayItem { Promotion = promo });
                }
            }
            cboPromotion.SelectedIndex = 0;
        }

        private void RecalculateTotal()
        {
            if (_suppressRecalc) return;
            if (cboProduct.SelectedItem is not LookupItem product) return;

            var nights = Math.Max(1, (dtpCheckOut.Value.Date - dtpCheckIn.Value.Date).Days);
            var subtotal = product.UnitPrice * nights;
            var discountPercent = cboPromotion.SelectedItem is PromoDisplayItem promoItem ? promoItem.Promotion.DiscountPercent : 0m;
            var total = subtotal - subtotal * (discountPercent / 100m);

            txtTotalAmount.Text = total.ToString("0.00", CultureInfo.InvariantCulture);
            lblBreakdown.Text = discountPercent > 0
                ? $"{nights} night(s) × ₱{product.UnitPrice:N2} = ₱{subtotal:N2}, less {discountPercent:0.##}% = ₱{total:N2}"
                : $"{nights} night(s) × ₱{product.UnitPrice:N2} = ₱{total:N2}";
        }

        // ------------------------------------------------------------ Grid data

        private async Task LoadReservationsAsync()
        {
            try
            {
                lblInfo.Text = "Loading reservations…";
                var url = $"/tenant/{_companyId}/reservations?includeArchived={chkShowArchived.Checked}";
                var reservations = await _http.GetFromJsonAsync<List<Reservation>>(url) ?? new();
                var customers = await _http.GetFromJsonAsync<List<Customer>>($"/tenant/{_companyId}/customers?includeArchived=true") ?? new();
                var products = await _http.GetFromJsonAsync<List<Product>>($"/tenant/{_companyId}/products?includeArchived=true") ?? new();

                var customerNames = customers.ToDictionary(c => c.CustomerId, c => c.FullName);
                var productNames = products.ToDictionary(p => p.ProductId, p => p.ProductName);

                _all = reservations.Select(r => new ReservationRow
                {
                    ReservationId = r.ReservationId,
                    CustomerId = r.CustomerId,
                    ProductId = r.ProductId,
                    CustomerName = customerNames.TryGetValue(r.CustomerId, out var cn) ? cn : $"#{r.CustomerId}",
                    ProductName = productNames.TryGetValue(r.ProductId, out var pn) ? pn : $"#{r.ProductId}",
                    CheckInDate = r.CheckInDate,
                    CheckOutDate = r.CheckOutDate,
                    Nights = Math.Max(0, (r.CheckOutDate.Date - r.CheckInDate.Date).Days),
                    Status = r.Status,
                    TotalAmount = r.TotalAmount,
                    IsArchived = r.IsArchived
                }).ToList();

                statTotal.Text = _all.Count.ToString();
                statPending.Text = _all.Count(r => r.Status == "Pending").ToString();
                statConfirmed.Text = _all.Count(r => r.Status is "Confirmed" or "CheckedIn" or "CheckedOut").ToString();
                statLost.Text = _all.Count(r => r.Status is "Cancelled" or "Refunded").ToString();

                ApplyFilter();
            }
            catch (Exception ex)
            {
                lblInfo.Text = "Failed to load reservations.";
                MessageBox.Show($"Failed to load reservations: {ex.Message}");
            }
        }

        private void ApplyFilter()
        {
            var term = txtSearch.Text.Trim();
            var status = cboStatusFilter.SelectedItem?.ToString() ?? "All statuses";

            var filtered = _all.Where(r =>
                (status == "All statuses" || r.Status == status) &&
                (!dtpFrom.Checked || r.CheckInDate.Date >= dtpFrom.Value.Date) &&
                (!dtpTo.Checked || r.CheckInDate.Date <= dtpTo.Value.Date) &&
                (term.Length == 0 ||
                 r.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 r.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 r.ReservationId.ToString() == term)
            ).ToList();

            _pager.Bind(filtered);
            lblInfo.Text = $"Showing {filtered.Count} of {_all.Count} reservation(s). Click a row to edit it.";
        }

        // ------------------------------------------------------------ Form

        private void OpenForm(ReservationRow? row)
        {
            _suppressRecalc = true;
            try
            {
                if (row is null)
                {
                    _selectedReservationId = null;
                    if (cboCustomer.Items.Count > 0) cboCustomer.SelectedIndex = 0;
                    if (cboProduct.Items.Count > 0) cboProduct.SelectedIndex = 0;
                    dtpCheckIn.Value = DateTime.Today;
                    dtpCheckOut.Value = DateTime.Today.AddDays(1);
                    cboStatus.SelectedIndex = 0;
                    RefreshEligiblePromotions();
                    lblFormTitle.Text = "New Reservation";
                    btnSave.Text = "Add Reservation";
                    btnArchive.Visible = false;
                }
                else
                {
                    _selectedReservationId = row.ReservationId;
                    SelectLookupById(cboCustomer, row.CustomerId);
                    RefreshEligiblePromotions();
                    SelectLookupById(cboProduct, row.ProductId);
                    dtpCheckIn.Value = row.CheckInDate;
                    dtpCheckOut.Value = row.CheckOutDate;
                    cboStatus.SelectedItem = row.Status;
                    lblFormTitle.Text = $"Edit Reservation #{row.ReservationId}";
                    btnSave.Text = "Save Changes";
                    btnArchive.Visible = !row.IsArchived;
                }
            }
            finally
            {
                _suppressRecalc = false;
            }

            if (row is null)
            {
                RecalculateTotal();
            }
            else
            {
                txtTotalAmount.Text = row.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture);
                lblBreakdown.Text = "Saved total. Change the room, dates or promotion to recalculate it.";
            }

            _v.Reset();
            card.Visible = true;
        }

        private void CloseForm()
        {
            _selectedReservationId = null;
            _v.Reset();
            card.Visible = false;
        }

        private static void SelectLookupById(ComboBox combo, int id)
        {
            foreach (var item in combo.Items)
                if (item is LookupItem lookup && lookup.Id == id) { combo.SelectedItem = lookup; return; }
        }

        private async Task SaveAsync()
        {
            if (!_v.ValidateAll()) return;

            var customer = (LookupItem)cboCustomer.SelectedItem!;
            var product = (LookupItem)cboProduct.SelectedItem!;

            var reservation = new Reservation
            {
                CustomerId = customer.Id,
                ProductId = product.Id,
                CheckInDate = dtpCheckIn.Value.Date,
                CheckOutDate = dtpCheckOut.Value.Date,
                Status = cboStatus.SelectedItem?.ToString() ?? "Pending",
                TotalAmount = decimal.Parse(txtTotalAmount.Text, CultureInfo.InvariantCulture)
            };

            var adding = _selectedReservationId is null;
            var response = adding
                ? await _http.PostAsJsonAsync($"/tenant/{_companyId}/reservations", reservation)
                : await _http.PutAsJsonAsync($"/tenant/{_companyId}/reservations/{_selectedReservationId}", reservation);

            if (response.IsSuccessStatusCode)
            {
                lblInfo.Text = adding ? "Reservation added." : "Reservation updated.";
                CloseForm();
                await LoadReservationsAsync();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                MessageBox.Show(await response.Content.ReadAsStringAsync(), "Booking Conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                lblInfo.Text = adding ? "Add failed." : "Update failed.";
                MessageBox.Show($"Failed to save reservation: {response.StatusCode}\n{await response.Content.ReadAsStringAsync()}");
            }
        }

        private async Task ArchiveAsync()
        {
            if (_selectedReservationId is null) return;
            if (MessageBox.Show("Archive this reservation? It will be hidden from the active list but the record is preserved.", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            var response = await _http.PutAsync($"/tenant/{_companyId}/reservations/{_selectedReservationId}/archive", null);
            if (response.IsSuccessStatusCode)
            {
                lblInfo.Text = "Reservation archived.";
                CloseForm();
                await LoadReservationsAsync();
            }
            else
            {
                lblInfo.Text = "Archive failed.";
                MessageBox.Show($"Failed to archive reservation: {response.StatusCode}");
            }
        }
    }
}