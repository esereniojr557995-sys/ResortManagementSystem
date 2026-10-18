using System.Net.Http.Json;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using ResortManagementSystem.Domain.Entities;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class BIDashboardControl : UserControl
    {
        private readonly HttpClient _http;
        private readonly int _companyId;
        private readonly string _companyName;

        private Label statCustomers = null!, statReservations = null!, statRevenue = null!, statPending = null!, statComplaints = null!, statStaff = null!;
        private Label lblStatus = null!;
        private Chart chartByStatus = null!;
        private Chart chartRevenueTrend = null!;
        private Chart chartFeedbackBreakdown = null!;
        private Chart chartTopRooms = null!;

        private List<Reservation> _cachedReservations = new();
        private List<Feedback> _cachedFeedback = new();
        private Dictionary<int, string> _cachedCustomerNames = new();
        private Dictionary<int, string> _cachedProductNames = new();
        private DateTimePicker dtpFrom = null!, dtpTo = null!;
        private string _planName = "";

        public BIDashboardControl(HttpClient http, int companyId, string companyName = "", string planName = "")
        {
            _http = http;
            _companyId = companyId;
            _companyName = companyName;
            _planName = planName;
            Dock = DockStyle.Fill;
            BackColor = ContentBg;
            Padding = new Padding(36, 28, 36, 28);
            BuildUi();
        }

        public async Task LoadAsync()
        {
            await LoadSummaryAsync();
            await LoadChartsAsync();
        }

        // ------------------------------------------------------------------ UI

        private static Panel MakeChartCard(string title, Control chart, int height)
        {
            var card = MakeCard(0, height);
            card.Padding = new Padding(16, 4, 16, 16);

            chart.Dock = DockStyle.Fill;

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = TextDark,
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(4, 12, 0, 0),
                UseMnemonic = false
            };

            // Fill control first, title last, so the title docks on top
            card.Controls.Add(chart);
            card.Controls.Add(titleLabel);
            return card;
        }

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = ContentBg };
            var topBar = MakeBanner("BI Dashboard & Reports", "Live KPIs and reservation trends — export everything as one PDF");

            var row1 = MakeStatsRow(
                ("Active Customers", Accent, ""),
                ("Total Reservations", Accent, ""),
                ("Confirmed Revenue", Accent, "confirmed + completed stays")
            );
            statCustomers = (Label)row1.Controls[0].Tag;
            statReservations = (Label)row1.Controls[1].Tag;
            statRevenue = (Label)row1.Controls[2].Tag;

            var spacer1 = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = ContentBg };

            var row2 = MakeStatsRow(
                ("Pending Reservations", AccentAmber, ""),
                ("Open Complaints", Danger, "needs attention"),
                ("Active Staff", Accent, "")
            );
            statPending = (Label)row2.Controls[0].Tag;
            statComplaints = (Label)row2.Controls[1].Tag;
            statStaff = (Label)row2.Controls[2].Tag;

            var spacer2 = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = ContentBg };

            // ---- Filter / action bar ----
            var refreshBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = ContentBg,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 6, 0, 0)
            };

            var btnRefresh = MakePillButton("Refresh", Accent, Color.White, 0, 0, 130);
            btnRefresh.Margin = new Padding(0, 0, 10, 0);
            var btnExportPdf = MakePillButton("Export Full Report (PDF)", AccentCoral, Color.White, 0, 0, 210);
            btnExportPdf.Margin = new Padding(0, 0, 10, 0);

            var lblFrom = new Label { Text = "From", AutoSize = true, ForeColor = TextMuted, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(20, 10, 6, 0) };
            dtpFrom = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddMonths(-6), Margin = new Padding(0, 6, 0, 0) };
            var lblTo = new Label { Text = "To", AutoSize = true, ForeColor = TextMuted, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(16, 10, 6, 0) };
            dtpTo = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddMonths(6), Margin = new Padding(0, 6, 0, 0) };
            var btnApplyDate = MakePillButton("Apply Filter", Accent, Color.White, 0, 0, 130);
            btnApplyDate.Margin = new Padding(16, 0, 0, 0);

            refreshBar.Controls.AddRange(new Control[] { btnRefresh, btnExportPdf, lblFrom, dtpFrom, lblTo, dtpTo, btnApplyDate });

            btnRefresh.Click += async (s, e) => await LoadAsync();
            btnExportPdf.Click += BtnExportPdf_Click;
            btnApplyDate.Click += async (s, e) =>
            {
                if (dtpFrom.Value.Date > dtpTo.Value.Date)
                {
                    MessageBox.Show("The 'From' date must be on or before the 'To' date.");
                    return;
                }
                await LoadChartsAsync();
            };

            var spacer3 = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = ContentBg };

            // ---- Charts row 1: feedback donut + reservations by status ----
            chartFeedbackBreakdown = BuildDonutChart();
            chartFeedbackBreakdown.MouseClick += ChartFeedback_MouseClick;
            chartFeedbackBreakdown.Cursor = Cursors.Hand;

            chartByStatus = BuildBarChart();
            chartByStatus.MouseClick += ChartByStatus_MouseClick;
            chartByStatus.Cursor = Cursors.Hand;

            var chartsRow = new Panel { Dock = DockStyle.Top, Height = 340, BackColor = ContentBg };
            var donutCard = MakeChartCard("Guest Feedback Breakdown", chartFeedbackBreakdown, 340);
            donutCard.Dock = DockStyle.Left;
            donutCard.Width = 400;
            var barCard = MakeChartCard("Reservations by Status  (click a bar for details)", chartByStatus, 340);
            barCard.Dock = DockStyle.Fill;
            var gap = new Panel { Dock = DockStyle.Left, Width = 20, BackColor = ContentBg };

            chartsRow.Controls.Add(barCard);   // Fill first
            chartsRow.Controls.Add(gap);
            chartsRow.Controls.Add(donutCard);

            var spacer3b = new Panel { Dock = DockStyle.Top, Height = 20, BackColor = ContentBg };

            // ---- Charts row 2: revenue trend ----
            chartRevenueTrend = BuildLineChart();
            chartRevenueTrend.MouseClick += ChartRevenue_MouseClick;
            chartRevenueTrend.Cursor = Cursors.Hand;

            var chartsRow2 = new Panel { Dock = DockStyle.Top, Height = 340, BackColor = ContentBg };
            var lineCard = MakeChartCard("Revenue Trend (Confirmed Reservations)  (click near a day for details)", chartRevenueTrend, 340);
            lineCard.Dock = DockStyle.Fill;
            chartsRow2.Controls.Add(lineCard);

            var spacer3c = new Panel { Dock = DockStyle.Top, Height = 20, BackColor = ContentBg };

            // ---- Charts row 3: most reserved rooms/services ----
            chartTopRooms = BuildTopRoomsChart();
            chartTopRooms.MouseClick += ChartTopRooms_MouseClick;
            chartTopRooms.Cursor = Cursors.Hand;

            var chartsRow3 = new Panel { Dock = DockStyle.Top, Height = 340, BackColor = ContentBg };
            var topRoomsCard = MakeChartCard("Most Reserved Rooms & Services  (click a bar for details)", chartTopRooms, 340);
            topRoomsCard.Dock = DockStyle.Fill;
            chartsRow3.Controls.Add(topRoomsCard);

            // Dock=Top: the LAST control added ends up on top
            header.Controls.Add(chartsRow3);
            header.Controls.Add(spacer3c);
            header.Controls.Add(chartsRow2);
            header.Controls.Add(spacer3b);
            header.Controls.Add(chartsRow);
            header.Controls.Add(spacer3);
            header.Controls.Add(refreshBar);
            header.Controls.Add(spacer2);
            header.Controls.Add(row2);
            header.Controls.Add(spacer1);
            header.Controls.Add(row1);
            header.Controls.Add(topBar);

            lblStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Text = "Ready.",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                UseMnemonic = false
            };

            var scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = ContentBg };
            scrollHost.Controls.Add(header);

            Controls.Add(scrollHost);   // Fill first
            Controls.Add(lblStatus);
        }

        // -------------------------------------------------------------- Charts

        private Chart BuildBarChart()
        {
            var chart = new Chart { BackColor = CardBg, MinimumSize = new Size(20, 20) };
            var area = new ChartArea();
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisY.MajorGrid.LineColor = BorderGray;
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 8.5f);
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 8.5f);
            area.AxisX.LabelStyle.IsStaggered = false;
            area.AxisX.Interval = 1;
            area.AxisY.Minimum = 0;
            area.AxisY.LabelStyle.Format = "0";
            chart.ChartAreas.Add(area);

            var series = new Series("Reservations")
            {
                ChartType = SeriesChartType.Column,
                Color = Accent,
                IsValueShownAsLabel = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ToolTip = "#VALX: #VAL reservation(s) - click for details"
            };
            series["PointWidth"] = "0.6";
            chart.Series.Add(series);
            chart.Legends.Clear();
            return chart;
        }

        private Chart BuildTopRoomsChart()
        {
            var chart = new Chart { BackColor = CardBg, MinimumSize = new Size(20, 20) };
            var area = new ChartArea();
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisY.MajorGrid.LineColor = BorderGray;
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 9f);
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 8.5f);
            area.AxisX.Interval = 1;
            area.AxisY.Minimum = 0;
            area.AxisY.LabelStyle.Format = "0";
            chart.ChartAreas.Add(area);

            var series = new Series("Bookings")
            {
                ChartType = SeriesChartType.Bar,
                Color = AccentDark,
                IsValueShownAsLabel = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ToolTip = "#VALX: #VAL booking(s) - click for details"
            };
            series["PointWidth"] = "0.7";
            chart.Series.Add(series);
            chart.Legends.Clear();
            return chart;
        }

        private Chart BuildLineChart()
        {
            var chart = new Chart { BackColor = CardBg, MinimumSize = new Size(20, 20) };
            var area = new ChartArea();
            area.AxisX.MajorGrid.LineColor = BorderGray;
            area.AxisY.MajorGrid.LineColor = BorderGray;
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 8.5f);
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 8.5f);
            area.AxisX.LabelStyle.Format = "MMM d";
            area.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            area.AxisY.LabelStyle.Format = "₱#,##0";
            area.AxisY.Minimum = 0;
            chart.ChartAreas.Add(area);

            chart.Series.Add(new Series("Revenue")
            {
                ChartType = SeriesChartType.Area,
                XValueType = ChartValueType.Date,
                Color = Color.FromArgb(120, Accent),
                BorderColor = AccentDark,
                BorderWidth = 2,
                ToolTip = "#VALX{MMM d, yyyy}: ₱#VAL{N0}"
            });
            chart.Legends.Clear();
            return chart;
        }

        private Chart BuildDonutChart()
        {
            var chart = new Chart { BackColor = CardBg, MinimumSize = new Size(20, 20) };
            chart.ChartAreas.Add(new ChartArea());

            var series = new Series("Feedback") { ChartType = SeriesChartType.Doughnut };
            series["PieLabelStyle"] = "Disabled";
            series["DoughnutRadius"] = "55";
            chart.Series.Add(series);

            chart.Legends.Add(new Legend { Font = new Font("Segoe UI", 9f), Docking = Docking.Bottom });
            return chart;
        }

        // ---------------------------------------------------------------- Data

        private async Task LoadSummaryAsync()
        {
            try
            {
                var summary = await _http.GetFromJsonAsync<ReportSummary>($"/tenant/{_companyId}/reports/summary");
                if (summary is null) return;

                statCustomers.Text = summary.TotalCustomers.ToString();
                statReservations.Text = summary.TotalReservations.ToString();
                statRevenue.Text = $"₱{summary.ConfirmedRevenue:N0}";
                statPending.Text = summary.PendingCount.ToString();
                statComplaints.Text = summary.OpenComplaints.ToString();
                statStaff.Text = summary.ActiveStaff.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load summary: {ex.Message}");
            }
        }

        private static bool IsRevenueStatus(string status) => status is "Confirmed" or "CheckedIn" or "CheckedOut";

        private async Task LoadChartsAsync()
        {
            try
            {
                lblStatus.Text = "Loading charts…";

                var from = dtpFrom.Value.Date;
                var to = dtpTo.Value.Date;

                var reservations = (await _http.GetFromJsonAsync<List<Reservation>>($"/tenant/{_companyId}/reservations") ?? new())
                    .Where(r => r.CheckInDate.Date >= from && r.CheckInDate.Date <= to)
                    .ToList();
                _cachedReservations = reservations;

                var customers = await _http.GetFromJsonAsync<List<Customer>>($"/tenant/{_companyId}/customers?includeArchived=true") ?? new();
                _cachedCustomerNames = customers.ToDictionary(c => c.CustomerId, c => c.FullName);

                var products = await _http.GetFromJsonAsync<List<Product>>($"/tenant/{_companyId}/products?includeArchived=true") ?? new();
                _cachedProductNames = products.ToDictionary(p => p.ProductId, p => p.ProductName);

                // ---- Bar chart: reservations by status ----
                var barSeries = chartByStatus.Series[0];
                barSeries.Points.Clear();
                foreach (var group in reservations.GroupBy(r => r.Status).OrderByDescending(g => g.Count()))
                {
                    var i = barSeries.Points.AddXY(group.Key, group.Count());
                    barSeries.Points[i].Color = group.Key switch
                    {
                        "Confirmed" or "CheckedIn" or "CheckedOut" => Accent,
                        "Pending" => AccentAmber,
                        "Cancelled" or "Refunded" => Danger,
                        _ => Color.Gray
                    };
                }

                // ---- Area chart: revenue by check-in date ----
                var lineSeries = chartRevenueTrend.Series[0];
                lineSeries.Points.Clear();
                var trend = reservations
                    .Where(r => IsRevenueStatus(r.Status))
                    .GroupBy(r => r.CheckInDate.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new { Date = g.Key, Total = g.Sum(r => r.TotalAmount) });

                foreach (var p in trend)
                    lineSeries.Points.AddXY(p.Date, p.Total);

                // ---- Donut: feedback breakdown ----
                var feedback = await _http.GetFromJsonAsync<List<Feedback>>($"/tenant/{_companyId}/feedback") ?? new();
                _cachedFeedback = feedback;

                var donutSeries = chartFeedbackBreakdown.Series[0];
                donutSeries.Points.Clear();

                void AddSlice(string label, int count, Color color)
                {
                    if (count <= 0) return;
                    var i = donutSeries.Points.AddXY(label, count);
                    var pt = donutSeries.Points[i];
                    pt.Color = color;
                    pt.Tag = label;
                    pt.LegendText = $"{label} ({count})";
                    pt.ToolTip = $"{label}: {count} - click for details";
                }

                AddSlice("Open Complaints", feedback.Count(f => f.IsComplaint && f.Status == "Open"), Danger);
                AddSlice("Resolved Complaints", feedback.Count(f => f.IsComplaint && f.Status == "Resolved"), Accent);
                AddSlice("General Feedback", feedback.Count(f => !f.IsComplaint), Color.FromArgb(0x9C, 0xA3, 0xAF));

                if (feedback.Count == 0)
                {
                    var i = donutSeries.Points.AddXY("No feedback yet", 1);
                    donutSeries.Points[i].Color = Color.FromArgb(0xEE, 0xF0, 0xF2);
                    donutSeries.Points[i].LegendText = "No feedback yet";
                }

                // ---- Top rooms/services ----
                var topRoomsSeries = chartTopRooms.Series[0];
                topRoomsSeries.Points.Clear();

                var topRooms = reservations
                    .Where(r => !r.IsArchived)
                    .GroupBy(r => r.ProductId)
                    .Select(g => new { ProductId = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(8)
                    .Reverse()
                    .ToList();

                foreach (var room in topRooms)
                {
                    var name = _cachedProductNames.TryGetValue(room.ProductId, out var n) ? n : $"#{room.ProductId}";
                    var i = topRoomsSeries.Points.AddXY(name, room.Count);
                    topRoomsSeries.Points[i].Tag = room.ProductId;
                }

                lblStatus.Text = $"Charts updated {DateTime.Now:MMM d, h:mm tt}.  Showing check-ins from {from:MMM d, yyyy} to {to:MMM d, yyyy}.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Failed to load charts.";
                MessageBox.Show($"Failed to load charts: {ex.Message}");
            }
        }

        // ---------------------------------------------------------- Drill-down

        // Columns are defined explicitly because StyleGrid sets AutoGenerateColumns = false
        private void ShowDrilldown<T>(string title, List<T> rows, params (string Prop, string Header, int Weight)[] columns)
        {
            var grid = new DataGridView { Dock = DockStyle.Fill };
            StyleGrid(grid);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            foreach (var (prop, header, weight) in columns)
                AddTextColumn(grid, prop, header, weight);
            grid.DataSource = rows;

            using var popup = new DrilldownForm(title, grid);
            popup.ShowDialog(FindForm());
        }

        private string CustomerName(int id) => _cachedCustomerNames.TryGetValue(id, out var n) ? n : $"Customer #{id}";
        private string ProductName(int id) => _cachedProductNames.TryGetValue(id, out var n) ? n : $"#{id}";

        private void ChartByStatus_MouseClick(object? sender, MouseEventArgs e)
        {
            var result = chartByStatus.HitTest(e.X, e.Y);
            if (result.ChartElementType != ChartElementType.DataPoint || result.PointIndex < 0) return;

            var status = chartByStatus.Series[0].Points[result.PointIndex].AxisLabel;

            var rows = _cachedReservations.Where(r => r.Status == status).Select(r => new
            {
                ReservationId = r.ReservationId,
                Customer = CustomerName(r.CustomerId),
                RoomService = ProductName(r.ProductId),
                CheckIn = r.CheckInDate.ToString("MM/dd/yyyy"),
                CheckOut = r.CheckOutDate.ToString("MM/dd/yyyy"),
                TotalAmount = r.TotalAmount.ToString("₱#,##0.00")
            }).ToList();

            ShowDrilldown($"{status} Reservations ({rows.Count})", rows,
                ("ReservationId", "ID", 50),
                ("Customer", "Customer", 120),
                ("RoomService", "Room / Service", 130),
                ("CheckIn", "Check-in", 90),
                ("CheckOut", "Check-out", 90),
                ("TotalAmount", "Total", 90));
        }

        private void ChartFeedback_MouseClick(object? sender, MouseEventArgs e)
        {
            var result = chartFeedbackBreakdown.HitTest(e.X, e.Y);
            if (result.ChartElementType != ChartElementType.DataPoint || result.PointIndex < 0) return;

            if (chartFeedbackBreakdown.Series[0].Points[result.PointIndex].Tag is not string label) return;

            var rows = _cachedFeedback.Where(f =>
                    (label == "Open Complaints" && f.IsComplaint && f.Status == "Open") ||
                    (label == "Resolved Complaints" && f.IsComplaint && f.Status == "Resolved") ||
                    (label == "General Feedback" && !f.IsComplaint))
                .Select(f => new
                {
                    FeedbackId = f.FeedbackId,
                    Customer = CustomerName(f.CustomerId),
                    Comments = f.Comments,
                    Logged = f.CreatedAt.ToString("MM/dd/yyyy h:mm tt")
                }).ToList();

            ShowDrilldown($"{label} ({rows.Count})", rows,
                ("FeedbackId", "ID", 40),
                ("Customer", "Customer", 110),
                ("Comments", "Comments", 280),
                ("Logged", "Logged", 120));
        }

        private void ChartTopRooms_MouseClick(object? sender, MouseEventArgs e)
        {
            var result = chartTopRooms.HitTest(e.X, e.Y);
            if (result.ChartElementType != ChartElementType.DataPoint || result.PointIndex < 0) return;

            var point = chartTopRooms.Series[0].Points[result.PointIndex];
            if (point.Tag is not int productId) return;

            var rows = _cachedReservations.Where(r => r.ProductId == productId && !r.IsArchived).Select(r => new
            {
                ReservationId = r.ReservationId,
                Customer = CustomerName(r.CustomerId),
                CheckIn = r.CheckInDate.ToString("MM/dd/yyyy"),
                CheckOut = r.CheckOutDate.ToString("MM/dd/yyyy"),
                Status = r.Status,
                TotalAmount = r.TotalAmount.ToString("₱#,##0.00")
            }).ToList();

            ShowDrilldown($"{point.AxisLabel} Bookings ({rows.Count})", rows,
                ("ReservationId", "ID", 50),
                ("Customer", "Customer", 120),
                ("CheckIn", "Check-in", 90),
                ("CheckOut", "Check-out", 90),
                ("Status", "Status", 90),
                ("TotalAmount", "Total", 90));
        }

        private void ChartRevenue_MouseClick(object? sender, MouseEventArgs e)
        {
            var series = chartRevenueTrend.Series[0];
            if (series.Points.Count == 0) return;

            var hit = chartRevenueTrend.HitTest(e.X, e.Y);
            if (hit.ChartElementType is not (ChartElementType.DataPoint or ChartElementType.PlottingArea or ChartElementType.Gridlines))
                return;

            DateTime day;
            try
            {
                var axis = chartRevenueTrend.ChartAreas[0].AxisX;
                var clicked = DateTime.FromOADate(axis.PixelPositionToValue(e.X)).Date;
                day = series.Points
                    .Select(p => DateTime.FromOADate(p.XValue).Date)
                    .OrderBy(d => Math.Abs((d - clicked).TotalDays))
                    .First();
            }
            catch { return; }

            var rows = _cachedReservations
                .Where(r => r.CheckInDate.Date == day && IsRevenueStatus(r.Status))
                .Select(r => new
                {
                    ReservationId = r.ReservationId,
                    Customer = CustomerName(r.CustomerId),
                    RoomService = ProductName(r.ProductId),
                    CheckOut = r.CheckOutDate.ToString("MM/dd/yyyy"),
                    Status = r.Status,
                    TotalAmount = r.TotalAmount.ToString("₱#,##0.00")
                }).ToList();

            ShowDrilldown($"Check-ins on {day:MMM d, yyyy} ({rows.Count})", rows,
                ("ReservationId", "ID", 50),
                ("Customer", "Customer", 120),
                ("RoomService", "Room / Service", 130),
                ("CheckOut", "Check-out", 90),
                ("Status", "Status", 90),
                ("TotalAmount", "Total", 90));
        }

        private async void BtnExportPdf_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "PDF Files|*.pdf",
                FileName = $"FullReport_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                lblStatus.Text = "Building the full report…";

                // Everything the report needs that this screen does not already hold
                var staff = await _http.GetFromJsonAsync<List<Staff>>($"/tenant/{_companyId}/staff") ?? new();
                var promos = await _http.GetFromJsonAsync<List<Promotion>>($"/tenant/{_companyId}/promotions") ?? new();
                var summary = await _http.GetFromJsonAsync<ReportSummary>($"/tenant/{_companyId}/reports/summary");

                var data = new AdminReportData
                {
                    CompanyName = string.IsNullOrWhiteSpace(_companyName) ? $"Company {_companyId}" : _companyName,
                    PlanName = _planName,
                    From = dtpFrom.Value.Date,
                    To = dtpTo.Value.Date,
                    Reservations = _cachedReservations,
                    Feedback = _cachedFeedback,
                    Staff = staff,
                    Promotions = promos,
                    CustomerNames = _cachedCustomerNames,
                    ProductNames = _cachedProductNames,
                    TotalCustomers = summary?.TotalCustomers ?? 0,
                    StatusChart = PdfReportService.CaptureChart(chartByStatus),
                    RevenueChart = PdfReportService.CaptureChart(chartRevenueTrend),
                    FeedbackChart = PdfReportService.CaptureChart(chartFeedbackBreakdown, 700, 420),
                    TopRoomsChart = PdfReportService.CaptureChart(chartTopRooms)
                };

                await Task.Run(() => PdfReportService.ExportAdminFullReport(sfd.FileName, data));

                lblStatus.Text = "Full report exported.";
                if (MessageBox.Show("Full report exported successfully.\n\nOpen it now?", "Export PDF",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Export failed.";
                MessageBox.Show($"Failed to export PDF: {ex.Message}");
            }
        }

        private class ReportSummary
        {
            public int TotalCustomers { get; set; }
            public int TotalReservations { get; set; }
            public decimal ConfirmedRevenue { get; set; }
            public int PendingCount { get; set; }
            public int OpenComplaints { get; set; }
            public int ActiveStaff { get; set; }
        }
    }
}