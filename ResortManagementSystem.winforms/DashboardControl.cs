using System.Net.Http.Json;
using System.Drawing;
using ResortManagementSystem.Domain.Entities;
using static ResortManagementSystem.Winforms.UiTheme;
using System.Windows.Forms.DataVisualization.Charting;

namespace ResortManagementSystem.Winforms
{
    public class DashboardControl : UserControl
    {
        private readonly HttpClient _http;
        private readonly int _companyId;
        private readonly UserRole _role;
        private readonly string _displayName;
        private readonly Action<Type> _navigate;

        private Label[] _statLabels = Array.Empty<Label>();
        private Label lblStatus = null!;
        private Panel alertList1 = null!;
        private Panel? alertList2;
        private Chart chartA = null!;
        private Chart chartB = null!;

        public DashboardControl(HttpClient http, int companyId, UserRole role, string displayName, Action<Type> navigate)
        {
            _http = http;
            _companyId = companyId;
            _role = role;
            _displayName = displayName;
            _navigate = navigate;
            Dock = DockStyle.Fill;
            BackColor = ContentBg;
            Padding = new Padding(36, 28, 36, 28);
            AutoScroll = true;                           // small screens can scroll instead of clipping
            AutoScrollMinSize = new Size(0, 940);
            BuildUi();
        }

        public async Task LoadAsync() => await LoadStatsAsync();

        // ================================================================ Layout

        private (string label, Color accent, string subtext)[] GetStatDefs() => _role switch
        {
            UserRole.Staff => new[]
            {
                ("Total Guests", Accent, "registered profiles"),
                ("Active Reservations", Accent, "confirmed or checked in"),
                ("Check-ins Today", AccentAmber, "arriving today"),
                ("Open Feedback", Danger, "awaiting follow-up")
            },
            UserRole.Manager => new[]
            {
                ("Active Reservations", Accent, "confirmed or checked in"),
                ("Pending Reservations", AccentAmber, "awaiting confirmation"),
                ("Open Complaints", Danger, "needs attention"),
                ("Active Promotions", Accent, "currently running")
            },
            UserRole.Admin => new[]
            {
                ("Active Staff", Accent, "accounts that can sign in"),
                ("Total Reservations", Accent, "all time"),
                ("Confirmed Revenue", Accent, "confirmed + completed stays"),
                ("Open Complaints", Danger, "needs attention")
            },
            UserRole.SuperAdmin => new[]
            {
                ("Total Companies", Accent, "provisioned tenant accounts"),
                ("Active Subscriptions", Accent, "can sign in today"),
                ("Inactive Subscriptions", Danger, "sign-in blocked"),
                ("Paid Plans", ChartBlue, "Small + Medium tiers")
            },
            _ => Array.Empty<(string, Color, string)>()
        };

        private string RoleTagline() => _role switch
        {
            UserRole.Staff => "Here's what's happening at the front desk.",
            UserRole.Manager => "An overview of what needs your attention.",
            UserRole.Admin => "Your business at a glance.",
            UserRole.SuperAdmin => "Platform-wide overview. Only company-level data is shown here.",
            _ => ""
        };

        private (string first, string? second) AlertTitles() => _role switch
        {
            UserRole.Staff => ("Today's Check-ins", "Upcoming Arrivals (next 7 days)"),
            UserRole.Manager => ("Pending Reservations", "Open Complaints"),
            UserRole.Admin => ("Open Complaints", "Team"),
            UserRole.SuperAdmin => ("Recent Companies", null),
            _ => ("Alerts", null)
        };

        private (string a, string b) ChartTitles() => _role switch
        {
            UserRole.Staff => ("Arrivals - Next 7 Days", "Reservations by Status"),
            UserRole.Manager => ("Reservations by Status", "Revenue by Month"),
            UserRole.Admin => ("Most Reserved Rooms & Services", "Revenue by Month"),
            UserRole.SuperAdmin => ("Companies by Subscription Tier", "Subscription Status"),
            _ => ("", "")
        };

        private void BuildUi()
        {
            var topBar = MakeBanner($"Welcome back, {_displayName}", RoleTagline());

            var defs = GetStatDefs();
            var statsRow = MakeStatsRow(defs);
            _statLabels = new Label[defs.Length];
            for (int i = 0; i < defs.Length; i++)
                _statLabels[i] = (Label)statsRow.Controls[i].Tag;

            var spacer1 = new Panel { Dock = DockStyle.Top, Height = 20, BackColor = ContentBg };

            // ---- Row 2: Quick Actions + one or two lists ----
            var actionsRow = new Panel { Dock = DockStyle.Top, Height = 260, BackColor = ContentBg };

            var actionsCard = MakeCard(0, 260);
            actionsCard.Width = 320;
            actionsCard.Dock = DockStyle.Left;
            actionsCard.Controls.Add(new Label { Text = "Quick Actions", Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = TextDark, Left = 20, Top = 16, AutoSize = true });
            BuildQuickActions(actionsCard);

            var gap = new Panel { Dock = DockStyle.Left, Width = 20, BackColor = ContentBg };

            var (title1, title2) = AlertTitles();
            var alertsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = title2 == null ? 1 : 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            alertsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            if (title2 != null) alertsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            var card1 = BuildAlertCard(title1, out alertList1);
            card1.Margin = new Padding(0, 0, title2 == null ? 0 : 10, 0);
            alertsTable.Controls.Add(card1, 0, 0);
            if (title2 != null)
            {
                var card2 = BuildAlertCard(title2, out var list2);
                alertList2 = list2;
                card2.Margin = new Padding(10, 0, 0, 0);
                alertsTable.Controls.Add(card2, 1, 0);
            }

            actionsRow.Controls.Add(alertsTable);   // Fill first
            actionsRow.Controls.Add(gap);
            actionsRow.Controls.Add(actionsCard);

            var spacer2 = new Panel { Dock = DockStyle.Top, Height = 20, BackColor = ContentBg };

            // ---- Row 3: two charts side by side ----
            switch (_role)
            {
                case UserRole.Staff:
                    chartA = BuildColumnChart(SeriesChartType.Column, "Arrivals", Accent);
                    chartB = BuildDonutChart();
                    break;
                case UserRole.Manager:
                    chartA = BuildDonutChart();
                    chartB = BuildColumnChart(SeriesChartType.Column, "Revenue", Accent, "₱#,##0", "₱#,##0");
                    break;
                case UserRole.Admin:
                    chartA = BuildColumnChart(SeriesChartType.Bar, "Bookings", Accent);
                    chartB = BuildColumnChart(SeriesChartType.Column, "Revenue", Accent, "₱#,##0", "₱#,##0");
                    break;
                default:
                    chartA = BuildColumnChart(SeriesChartType.Column, "Companies", Accent);
                    chartB = BuildDonutChart();
                    break;
            }

            var (chartTitleA, chartTitleB) = ChartTitles();
            var chartsRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 310,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            chartsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            chartsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            var chartCardA = BuildChartCard(chartTitleA, chartA);
            chartCardA.Margin = new Padding(0, 0, 10, 0);
            var chartCardB = BuildChartCard(chartTitleB, chartB);
            chartCardB.Margin = new Padding(10, 0, 0, 0);
            chartsRow.Controls.Add(chartCardA, 0, 0);
            chartsRow.Controls.Add(chartCardB, 1, 0);

            lblStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                Text = "Loading…",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Padding = new Padding(0, 8, 0, 0),
                UseMnemonic = false
            };

            // Dock = Top: the LAST control added ends up on top
            Controls.Add(lblStatus);
            Controls.Add(chartsRow);
            Controls.Add(spacer2);
            Controls.Add(actionsRow);
            Controls.Add(spacer1);
            Controls.Add(statsRow);
            Controls.Add(topBar);
        }

        private static Panel BuildAlertCard(string title, out Panel listPanel)
        {
            var card = MakeCard(0, 260);
            card.Dock = DockStyle.Fill;

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = TextDark,
                Left = 20,
                Top = 16,
                AutoSize = true,
                UseMnemonic = false
            };

            listPanel = new Panel
            {
                Left = 20,
                Top = 48,
                Width = card.Width - 40,
                Height = 200,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                AutoScroll = true
            };

            card.Controls.Add(listPanel);
            card.Controls.Add(titleLabel);
            return card;
        }

        private static Panel BuildChartCard(string title, Chart chart)
        {
            var card = MakeCard(0, 310);
            card.Dock = DockStyle.Fill;
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

        private void BuildQuickActions(Panel card)
        {
            int top = 56;
            void AddAction(string text, Type targetType)
            {
                var btn = MakePillButton(text, Accent, Color.White, 20, top, 280);
                btn.Click += (s, e) => _navigate(targetType);
                card.Controls.Add(btn);
                top += 44;
            }

            switch (_role)
            {
                case UserRole.Staff:
                    AddAction("+ Add Reservation", typeof(ReservationsControl));
                    AddAction("+ Add Customer", typeof(CustomersControl));
                    AddAction("Log Guest Feedback", typeof(FeedbackControl));
                    break;
                case UserRole.Manager:
                    AddAction("Resolve Complaints", typeof(FeedbackControl));
                    AddAction("Manage Promotions", typeof(PromotionsControl));
                    AddAction("View Reservations", typeof(ReservationsControl));
                    break;
                case UserRole.Admin:
                    AddAction("Manage Staff", typeof(StaffControl));
                    AddAction("View Reports", typeof(BIDashboardControl));
                    break;
                case UserRole.SuperAdmin:
                    AddAction("Manage Companies", typeof(CompaniesControl));
                    AddAction("Platform BI Dashboard", typeof(MasterBIDashboardControl));
                    break;
            }
        }

        // ================================================================ Charts

        private Chart BuildColumnChart(SeriesChartType type, string name, Color color,
            string axisFormat = "0", string? labelFormat = null)
        {
            var chart = new Chart { BackColor = CardBg, MinimumSize = new Size(200, 150) };
            var area = new ChartArea();
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisY.MajorGrid.LineColor = BorderGray;
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 8.5f);
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 8.5f);
            area.AxisX.Interval = 1;
            area.AxisY.Minimum = 0;                      // removes the "-0" label
            area.AxisY.LabelStyle.Format = axisFormat;
            chart.ChartAreas.Add(area);

            var series = new Series(name)
            {
                ChartType = type,
                Color = color,
                IsValueShownAsLabel = true,              // value at the end of each bar
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ToolTip = "#VALX: #VAL"
            };
            if (labelFormat != null) series.LabelFormat = labelFormat;
            series["PointWidth"] = "0.6";
            chart.Series.Add(series);
            chart.Legends.Clear();
            return chart;
        }

        private Chart BuildDonutChart()
        {
            var chart = new Chart { BackColor = CardBg, MinimumSize = new Size(200, 150) };
            chart.ChartAreas.Add(new ChartArea());

            var series = new Series("Slices") { ChartType = SeriesChartType.Doughnut };
            series["PieLabelStyle"] = "Disabled";
            series["DoughnutRadius"] = "55";
            chart.Series.Add(series);

            chart.Legends.Add(new Legend { Font = new Font("Segoe UI", 9f), Docking = Docking.Bottom });
            return chart;
        }

        private static void SetEmptyNote(Chart chart, bool empty)
        {
            chart.Titles.Clear();
            if (!empty) return;
            chart.Titles.Add(new Title("No data to show yet", Docking.Top, new Font("Segoe UI", 9f, FontStyle.Italic), TextMuted));
        }

        private static void FillDonut(Chart chart, IEnumerable<(string label, int count, Color color)> slices)
        {
            var series = chart.Series[0];
            series.Points.Clear();
            bool any = false;

            foreach (var (label, count, color) in slices)
            {
                if (count <= 0) continue;
                any = true;
                var i = series.Points.AddXY(label, count);
                var pt = series.Points[i];
                pt.Color = color;
                pt.LegendText = $"{label} ({count})";
                pt.ToolTip = $"{label}: {count}";
            }

            if (!any)
            {
                var i = series.Points.AddXY("No data", 1);
                series.Points[i].Color = Color.FromArgb(0xEE, 0xF0, 0xF2);
                series.Points[i].LegendText = "No data yet";
            }
        }

        private static bool IsRevenueStatus(string status) => status is "Confirmed" or "CheckedIn" or "CheckedOut";

        private static Color StatusColor(string status) => status switch
        {
            "Confirmed" or "CheckedIn" or "CheckedOut" => Accent,
            "Pending" => AccentAmber,
            "Cancelled" or "Refunded" => Danger,
            _ => ChartGray
        };

        private static void FillStatusDonut(Chart chart, List<Reservation> reservations)
        {
            FillDonut(chart, reservations
                .GroupBy(r => r.Status)
                .OrderByDescending(g => g.Count())
                .Select(g => (g.Key, g.Count(), StatusColor(g.Key))));
        }

        // Last 5 months + this month + next month, by check-in month
        private static void FillRevenueByMonth(Chart chart, List<Reservation> reservations)
        {
            var series = chart.Series[0];
            series.Points.Clear();

            var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var start = thisMonth.AddMonths(-5);
            bool anyRevenue = false;

            for (int m = 0; m < 7; m++)
            {
                var month = start.AddMonths(m);
                decimal total = reservations
                    .Where(r => IsRevenueStatus(r.Status)
                                && r.CheckInDate.Year == month.Year
                                && r.CheckInDate.Month == month.Month)
                    .Sum(r => r.TotalAmount);

                var i = series.Points.AddXY(month.ToString("MMM yyyy"), total);
                var pt = series.Points[i];
                if (month == thisMonth) pt.Color = AccentDark;
                if (total <= 0) pt.IsValueShownAsLabel = false;   // no "₱0" clutter
                else anyRevenue = true;
            }

            SetEmptyNote(chart, !anyRevenue);
        }

        private static void FillArrivals(Chart chart, List<Reservation> reservations)
        {
            var series = chart.Series[0];
            series.Points.Clear();
            bool any = false;

            for (int d = 0; d < 7; d++)
            {
                var day = DateTime.Today.AddDays(d);
                int count = reservations.Count(r => r.CheckInDate.Date == day && r.Status is not ("Cancelled" or "Refunded"));
                if (count > 0) any = true;

                var i = series.Points.AddXY(d == 0 ? "Today" : day.ToString("ddd d"), count);
                if (d == 0) series.Points[i].Color = AccentAmber;
            }

            SetEmptyNote(chart, !any);
        }

        private static void FillTopRooms(Chart chart, List<Reservation> reservations, Dictionary<int, string> productNames)
        {
            var series = chart.Series[0];
            series.Points.Clear();

            var top = reservations
                .Where(r => !r.IsArchived)
                .GroupBy(r => r.ProductId)
                .Select(g => new { ProductId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .Reverse()     // bar charts draw the first point at the bottom, so the biggest ends up on top
                .ToList();

            foreach (var room in top)
            {
                var name = productNames.TryGetValue(room.ProductId, out var n) ? n : $"#{room.ProductId}";
                series.Points.AddXY(name, room.Count);
            }

            SetEmptyNote(chart, top.Count == 0);
        }

        // ================================================================ Alert lists

        private static void RenderAlertList(Panel target,
            List<(string primary, string secondary, string badgeText, (Color Bg, Color Fg) badgeColor)> items)
        {
            target.Controls.Clear();
            if (items.Count == 0)
            {
                target.Controls.Add(new Label { Text = "Nothing to show right now.", ForeColor = TextMuted, Font = new Font("Segoe UI", 9f, FontStyle.Italic), Left = 4, Top = 4, AutoSize = true });
                return;
            }

            int top = 0;
            foreach (var (primary, secondary, badgeText, badgeColor) in items)
            {
                var row = new Panel
                {
                    Left = 0,
                    Top = top,
                    Width = Math.Max(300, target.ClientSize.Width - 4),
                    Height = 48,
                    BorderStyle = BorderStyle.None,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };
                var line = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = BorderGray };
                var primaryLabel = new Label { Text = primary, ForeColor = TextDark, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Left = 0, Top = 6, AutoSize = true, UseMnemonic = false };
                var secondaryLabel = new Label { Text = secondary, ForeColor = TextMuted, Font = new Font("Segoe UI", 8.5f), Left = 0, Top = 26, AutoSize = true, UseMnemonic = false };

                var badge = new Label
                {
                    Text = badgeText,
                    BackColor = badgeColor.Bg,
                    ForeColor = badgeColor.Fg,
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                    AutoSize = true,
                    UseMnemonic = false,
                    Padding = new Padding(10, 4, 10, 4),
                    Top = 12
                };

                row.Controls.AddRange(new Control[] { primaryLabel, secondaryLabel, badge, line });

                // Set the position now AND on resize (resize alone never fired at first)
                void PlaceBadge() => badge.Left = Math.Max(0, row.Width - badge.PreferredWidth - 4);
                PlaceBadge();
                row.Resize += (s, e) => PlaceBadge();

                target.Controls.Add(row);
                top += 48;
            }
        }

        private static string ShortText(string? text, int max = 40)
        {
            text ??= "";
            return text.Length > max ? text[..max] + "…" : text;
        }

        private static string NameOf(Dictionary<int, string> map, int id, string fallbackPrefix)
            => map.TryGetValue(id, out var n) ? n : $"{fallbackPrefix} #{id}";

        private static (Color Bg, Color Fg) TierBadge(string tier) => tier switch
        {
            "Medium" => BadgeBlue,
            "Small" => BadgeGreen,
            _ => BadgeGray
        };

        private static (Color Bg, Color Fg) RoleBadge(string role) => role switch
        {
            "Admin" => BadgeBlue,
            "Manager" => BadgeGreen,
            _ => BadgeGray
        };

        // ================================================================ Data

        private async Task LoadStatsAsync()
        {
            try
            {
                switch (_role)
                {
                    case UserRole.Staff: await LoadStaffDashboardAsync(); break;
                    case UserRole.Manager: await LoadManagerDashboardAsync(); break;
                    case UserRole.Admin: await LoadAdminDashboardAsync(); break;
                    case UserRole.SuperAdmin: await LoadSuperAdminDashboardAsync(); break;
                }
                lblStatus.Text = $"Updated {DateTime.Now:MMM d, h:mm tt}.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Failed to load dashboard.";
                MessageBox.Show($"Failed to load dashboard: {ex.Message}");
            }
        }

        private async Task LoadStaffDashboardAsync()
        {
            var customers = await _http.GetFromJsonAsync<List<Customer>>($"/tenant/{_companyId}/customers") ?? new();
            var reservations = await _http.GetFromJsonAsync<List<Reservation>>($"/tenant/{_companyId}/reservations") ?? new();
            var feedback = await _http.GetFromJsonAsync<List<Feedback>>($"/tenant/{_companyId}/feedback") ?? new();
            var products = await _http.GetFromJsonAsync<List<Product>>($"/tenant/{_companyId}/products?includeArchived=true") ?? new();
            var customerNames = customers.ToDictionary(c => c.CustomerId, c => c.FullName);
            var productNames = products.ToDictionary(p => p.ProductId, p => p.ProductName);
            var today = DateTime.Today;

            var checkInsToday = reservations
                .Where(r => r.CheckInDate.Date == today && r.Status == "Confirmed")
                .ToList();

            _statLabels[0].Text = customers.Count.ToString();
            _statLabels[1].Text = reservations.Count(r => r.Status is "Confirmed" or "CheckedIn").ToString();
            _statLabels[2].Text = checkInsToday.Count.ToString();
            _statLabels[3].Text = feedback.Count(f => f.Status == "Open").ToString();

            RenderAlertList(alertList1, checkInsToday.Select(r => (
                NameOf(customerNames, r.CustomerId, "Customer"),
                $"{NameOf(productNames, r.ProductId, "Room")} · Reservation #{r.ReservationId}",
                "Today",
                BadgeAmber
            )).ToList());

            var upcoming = reservations
                .Where(r => r.CheckInDate.Date > today
                            && r.CheckInDate.Date <= today.AddDays(7)
                            && (r.Status is "Confirmed" or "Pending"))
                .OrderBy(r => r.CheckInDate)
                .Take(6)
                .Select(r => (
                    NameOf(customerNames, r.CustomerId, "Customer"),
                    $"{NameOf(productNames, r.ProductId, "Room")} · {(r.CheckOutDate.Date - r.CheckInDate.Date).Days} night(s)",
                    r.CheckInDate.Date == today.AddDays(1) ? "Tomorrow" : r.CheckInDate.ToString("MMM d"),
                    BadgeBlue))
                .ToList();
            RenderAlertList(alertList2!, upcoming);

            FillArrivals(chartA, reservations);
            FillStatusDonut(chartB, reservations);
        }

        private async Task LoadManagerDashboardAsync()
        {
            var reservations = await _http.GetFromJsonAsync<List<Reservation>>($"/tenant/{_companyId}/reservations") ?? new();
            var feedback = await _http.GetFromJsonAsync<List<Feedback>>($"/tenant/{_companyId}/feedback") ?? new();
            var promotions = await _http.GetFromJsonAsync<List<Promotion>>($"/tenant/{_companyId}/promotions") ?? new();
            var customers = await _http.GetFromJsonAsync<List<Customer>>($"/tenant/{_companyId}/customers") ?? new();
            var products = await _http.GetFromJsonAsync<List<Product>>($"/tenant/{_companyId}/products?includeArchived=true") ?? new();
            var customerNames = customers.ToDictionary(c => c.CustomerId, c => c.FullName);
            var productNames = products.ToDictionary(p => p.ProductId, p => p.ProductName);

            var openComplaints = feedback.Where(f => f.IsComplaint && f.Status == "Open").ToList();
            var pending = reservations.Where(r => r.Status == "Pending").OrderBy(r => r.CheckInDate).ToList();

            _statLabels[0].Text = reservations.Count(r => r.Status is "Confirmed" or "CheckedIn").ToString();
            _statLabels[1].Text = pending.Count.ToString();
            _statLabels[2].Text = openComplaints.Count.ToString();
            _statLabels[3].Text = promotions.Count(p => p.IsActive).ToString();

            RenderAlertList(alertList1, pending.Take(5).Select(r => (
                NameOf(customerNames, r.CustomerId, "Customer"),
                $"Check-in {r.CheckInDate:MMM d} · {NameOf(productNames, r.ProductId, "Room")} · ₱{r.TotalAmount:N0}",
                "Pending",
                BadgeAmber
            )).ToList());

            RenderAlertList(alertList2!, openComplaints.Take(5).Select(f => (
                NameOf(customerNames, f.CustomerId, "Customer"),
                $"{f.CreatedAt:MMM d} · {ShortText(f.Comments)}",
                "Open",
                BadgeRed
            )).ToList());

            FillStatusDonut(chartA, reservations);
            FillRevenueByMonth(chartB, reservations);
        }

        private async Task LoadAdminDashboardAsync()
        {
            var staff = await _http.GetFromJsonAsync<List<Staff>>($"/tenant/{_companyId}/staff") ?? new();
            var summary = await _http.GetFromJsonAsync<ReportSummary>($"/tenant/{_companyId}/reports/summary");
            var feedback = await _http.GetFromJsonAsync<List<Feedback>>($"/tenant/{_companyId}/feedback") ?? new();
            var customers = await _http.GetFromJsonAsync<List<Customer>>($"/tenant/{_companyId}/customers") ?? new();
            var reservations = await _http.GetFromJsonAsync<List<Reservation>>($"/tenant/{_companyId}/reservations") ?? new();
            var products = await _http.GetFromJsonAsync<List<Product>>($"/tenant/{_companyId}/products?includeArchived=true") ?? new();
            var customerNames = customers.ToDictionary(c => c.CustomerId, c => c.FullName);
            var productNames = products.ToDictionary(p => p.ProductId, p => p.ProductName);

            var openComplaints = feedback.Where(f => f.IsComplaint && f.Status == "Open").ToList();

            _statLabels[0].Text = staff.Count(s => s.IsActive).ToString();
            _statLabels[1].Text = (summary?.TotalReservations ?? 0).ToString();
            _statLabels[2].Text = $"₱{(summary?.ConfirmedRevenue ?? 0):N0}";
            _statLabels[3].Text = openComplaints.Count.ToString();

            RenderAlertList(alertList1, openComplaints.Take(5).Select(f => (
                NameOf(customerNames, f.CustomerId, "Customer"),
                $"{f.CreatedAt:MMM d} · {ShortText(f.Comments)}",
                "Open",
                BadgeRed
            )).ToList());

            RenderAlertList(alertList2!, staff.Where(s => s.IsActive).Take(5).Select(s => (
                s.FullName,
                s.Email,
                s.Role,
                RoleBadge(s.Role)
            )).ToList());

            FillTopRooms(chartA, reservations, productNames);
            FillRevenueByMonth(chartB, reservations);
        }

        private async Task LoadSuperAdminDashboardAsync()
        {
            var companies = await _http.GetFromJsonAsync<List<Company>>("/companies") ?? new();
            int active = companies.Count(c => c.IsSubscriptionActive);

            _statLabels[0].Text = companies.Count.ToString();
            _statLabels[1].Text = active.ToString();
            _statLabels[2].Text = (companies.Count - active).ToString();
            _statLabels[3].Text = companies.Count(c => c.SubscriptionTier is "Small" or "Medium").ToString();

            RenderAlertList(alertList1, companies.OrderByDescending(c => c.CompanyId).Take(5).Select(c => (
                c.CompanyName,
                $"{c.CompanyCode} · {c.SubscriptionTier} plan",
                c.IsSubscriptionActive ? c.SubscriptionTier : "Inactive",
                c.IsSubscriptionActive ? TierBadge(c.SubscriptionTier) : BadgeRed
            )).ToList());

            // Company-level data only, no tenant records
            var tiers = chartA.Series[0];
            tiers.Points.Clear();
            void AddTier(string tier, Color color)
            {
                var i = tiers.Points.AddXY(tier, companies.Count(c => c.SubscriptionTier == tier));
                tiers.Points[i].Color = color;
            }
            AddTier("Micro", ChartGray);
            AddTier("Small", Accent);
            AddTier("Medium", ChartBlue);
            SetEmptyNote(chartA, companies.Count == 0);

            FillDonut(chartB, new (string, int, Color)[]
            {
                ("Active", active, Accent),
                ("Inactive", companies.Count - active, Danger)
            });
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