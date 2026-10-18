using System.Net.Http.Json;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using static ResortManagementSystem.Winforms.UiTheme;

namespace ResortManagementSystem.Winforms
{
    public class MasterBIDashboardControl : UserControl
    {
        private readonly HttpClient _http;
        private Label statCompanies = null!, statCustomers = null!, statReservations = null!, statRevenue = null!;
        private Label lblStatus = null!;
        private Chart chartTiers = null!;

        public MasterBIDashboardControl(HttpClient http)
        {
            _http = http;
            Dock = DockStyle.Fill;
            BackColor = ContentBg;
            Padding = new Padding(36, 28, 36, 28);
            BuildUi();
        }

        public async Task LoadAsync() => await LoadDataAsync();

        private void BuildUi()
        {
            var topBar = MakeBanner("Platform BI Dashboard", "Aggregate KPIs across all tenant companies");

            var row = MakeStatsRow(
                ("Total Companies", Accent, "provisioned tenant accounts"),
                ("Total Customers", Accent, "across all tenants"),
                ("Total Reservations", Accent, "across all tenants"),
                ("Total Revenue", Accent, "confirmed + completed stays")
            );
            statCompanies = (Label)row.Controls[0].Tag;
            statCustomers = (Label)row.Controls[1].Tag;
            statReservations = (Label)row.Controls[2].Tag;
            statRevenue = (Label)row.Controls[3].Tag;

            var btnRefresh = MakePillButton("Refresh", Accent, Color.White, 0, 0, 130);
            var refreshBar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = ContentBg };
            btnRefresh.Top = 8;
            refreshBar.Controls.Add(btnRefresh);
            btnRefresh.Click += async (s, e) => await LoadDataAsync();

            var btnExport = MakePillButton("Export Platform Report (PDF)", AccentCoral, Color.White, 142, 8, 250);
            refreshBar.Controls.Add(btnExport);
            btnExport.Click += async (s, e) => await ExportPdfAsync();

            lblStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                Text = "Ready.",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Padding = new Padding(0, 8, 0, 0),
                UseMnemonic = false
            };

            // ---- Tier chart card ----
            chartTiers = new Chart { BackColor = CardBg, MinimumSize = new Size(300, 150) };
            var area = new ChartArea();
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisY.MajorGrid.LineColor = BorderGray;
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 9f);
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 8.5f);
            area.AxisX.Interval = 1;
            area.AxisY.Minimum = 0;
            area.AxisY.LabelStyle.Format = "0";
            chartTiers.ChartAreas.Add(area);

            var series = new Series("Companies")
            {
                ChartType = SeriesChartType.Column,
                Color = Accent,
                IsValueShownAsLabel = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ToolTip = "#VALX tier: #VAL company(ies)"
            };
            series["PointWidth"] = "0.5";
            chartTiers.Series.Add(series);
            chartTiers.Legends.Clear();

            var chartCard = MakeCard(0, 300);
            chartCard.Dock = DockStyle.Top;
            chartCard.Padding = new Padding(16, 4, 16, 16);
            chartTiers.Dock = DockStyle.Fill;
            var chartTitle = new Label
            {
                Text = "Companies by Subscription Tier",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = TextDark,
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(4, 12, 0, 0),
                UseMnemonic = false
            };
            chartCard.Controls.Add(chartTiers);   // Fill first
            chartCard.Controls.Add(chartTitle);

            var privacyNote = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = "Only aggregate totals are shown. Individual guests, reservations and staff records stay private to each company.",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Padding = new Padding(0, 10, 0, 0),
                UseMnemonic = false
            };

            var spacer = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = ContentBg };

            // Dock = Top: the last control added ends up on top.
            Controls.Add(privacyNote);
            Controls.Add(chartCard);
            Controls.Add(spacer);
            Controls.Add(lblStatus);
            Controls.Add(refreshBar);
            Controls.Add(row);
            Controls.Add(topBar);
        }

        private async Task LoadDataAsync()
        {
            try
            {
                lblStatus.Text = "Loading platform-wide report…";
                var data = await _http.GetFromJsonAsync<AggregateReport>("/admin/reports/aggregate");
                if (data is null) return;

                statCompanies.Text = data.totalCompanies.ToString();
                statCustomers.Text = data.totalCustomers.ToString();
                statReservations.Text = data.totalReservations.ToString();
                statRevenue.Text = $"₱{data.totalRevenue:N0}";

                var points = chartTiers.Series[0].Points;
                points.Clear();
                void AddTier(string tier, Color color)
                {
                    var count = data.tierBreakdown?.FirstOrDefault(t => t.Tier == tier)?.Count ?? 0;
                    var i = points.AddXY(tier, count);
                    points[i].Color = color;
                }
                AddTier("Micro", ChartGray);
                AddTier("Small", Accent);
                AddTier("Medium", ChartBlue);

                lblStatus.Text = $"Updated {DateTime.Now:MMM d, h:mm tt}.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Failed to load platform report.";
                MessageBox.Show($"Failed to load platform report: {ex.Message}");
            }
        }

        private async Task ExportPdfAsync()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "PDF Files|*.pdf",
                FileName = $"PlatformReport_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                lblStatus.Text = "Building the platform report…";
                var data = await _http.GetFromJsonAsync<AggregateReport>("/admin/reports/aggregate");
                var companies = await _http.GetFromJsonAsync<List<ResortManagementSystem.Domain.Entities.Company>>("/companies") ?? new();
                if (data is null) return;

                var report = new PlatformReportData
                {
                    TotalCompanies = data.totalCompanies,
                    TotalCustomers = data.totalCustomers,
                    TotalReservations = data.totalReservations,
                    TotalRevenue = data.totalRevenue,
                    Companies = companies,
                    TierChart = PdfReportService.CaptureChart(chartTiers, 800, 360)
                };

                await Task.Run(() => PdfReportService.ExportPlatformReport(sfd.FileName, report));

                lblStatus.Text = "Platform report exported.";
                if (MessageBox.Show("Platform report exported successfully.\n\nOpen it now?", "Export PDF",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Export failed.";
                MessageBox.Show($"Failed to export PDF: {ex.Message}");
            }
        }

        private class TierItem
        {
            public string Tier { get; set; } = "";
            public int Count { get; set; }
        }

        private class AggregateReport
        {
            public int totalCompanies { get; set; }
            public int totalCustomers { get; set; }
            public int totalReservations { get; set; }
            public decimal totalRevenue { get; set; }
            public List<TierItem>? tierBreakdown { get; set; }
        }
    }
}