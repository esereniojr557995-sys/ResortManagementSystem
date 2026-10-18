using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ResortManagementSystem.Domain.Entities;

namespace ResortManagementSystem.Winforms
{
    // ------------------------------------------------------------------
    // Everything the Admin "Full Report" needs. The screen fills this in and hands it over.
    // ------------------------------------------------------------------
    public class AdminReportData
    {
        public string CompanyName { get; set; } = "";
        public string PlanName { get; set; } = "";
        public DateTime From { get; set; }
        public DateTime To { get; set; }

        public List<Reservation> Reservations { get; set; } = new();
        public List<Feedback> Feedback { get; set; } = new();
        public List<Staff> Staff { get; set; } = new();
        public List<Promotion> Promotions { get; set; } = new();
        public Dictionary<int, string> CustomerNames { get; set; } = new();
        public Dictionary<int, string> ProductNames { get; set; } = new();

        public int TotalCustomers { get; set; }

        // PNG snapshots of the on-screen charts (optional)
        public byte[]? StatusChart { get; set; }
        public byte[]? RevenueChart { get; set; }
        public byte[]? FeedbackChart { get; set; }
        public byte[]? TopRoomsChart { get; set; }
    }

    // Everything the Super Admin "Platform Report" needs
    public class PlatformReportData
    {
        public int TotalCompanies { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalReservations { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<Company> Companies { get; set; } = new();
        public byte[]? TierChart { get; set; }

        // Filled in once subscription records exist (company, plan, start, end, status)
        public List<string[]> SubscriptionHistory { get; set; } = new();
    }

    public static class PdfReportService
    {
        static PdfReportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;

            // Use the fonts installed on this PC (Segoe UI ships with Windows and has the peso sign)
            QuestPDF.Settings.UseSystemFonts = true;

            // If a font is ever missing, fall back instead of crashing the export
            QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
        }

        private const string Navy = "#0A2E4D";
        private const string Teal = "#0F8B8D";
        private const string Coral = "#FF7A59";
        private const string Sand = "#F7F3EA";
        private const string Ink = "#17323D";
        private const string Muted = "#6B7B80";
        private const string Line = "#E8E1D3";

        private static bool IsRevenue(string status) => status is "Confirmed" or "CheckedIn" or "CheckedOut";

        // ------------------------------------------------------------------
        // Chart -> PNG (so the PDF shows the same charts as the screen)
        // ------------------------------------------------------------------
        public static byte[]? CaptureChart(Chart chart, int width = 900, int height = 420)
        {
            try
            {
                var oldDock = chart.Dock;
                System.Drawing.Size oldSize = chart.Size;
                System.Drawing.Point oldLoc = chart.Location;

                chart.Dock = DockStyle.None;
                chart.Size = new System.Drawing.Size(width, height);
                using var ms = new MemoryStream();
                chart.SaveImage(ms, ChartImageFormat.Png);

                chart.Dock = oldDock;
                chart.Size = oldSize;
                chart.Location = oldLoc;
                return ms.ToArray();
            }
            catch
            {
                return null;   // a missing chart image must never stop the export
            }
        }

        // ================================================================
        // ADMIN: FULL REPORT
        // ================================================================
        public static void ExportAdminFullReport(string filePath, AdminReportData d)
        {
            var live = d.Reservations.Where(r => !r.IsArchived).ToList();
            var revenueRes = live.Where(r => IsRevenue(r.Status)).ToList();
            decimal revenue = revenueRes.Sum(r => r.TotalAmount);
            int pending = live.Count(r => r.Status == "Pending");
            int cancelled = live.Count(r => r.Status is "Cancelled" or "Refunded");
            int openComplaints = d.Feedback.Count(f => f.IsComplaint && f.Status == "Open");
            int activeStaff = d.Staff.Count(s => s.IsActive && !s.IsArchived);

            string RoomName(int id) => d.ProductNames.TryGetValue(id, out var n) ? n : $"#{id}";
            string GuestName(int id) => d.CustomerNames.TryGetValue(id, out var n) ? n : $"Customer #{id}";

            var byStatus = live.GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(r => r.TotalAmount) })
                .OrderByDescending(x => x.Count).ToList();

            var byMonth = revenueRes.GroupBy(r => new DateTime(r.CheckInDate.Year, r.CheckInDate.Month, 1))
                .OrderBy(g => g.Key)
                .Select(g => new { Month = g.Key, Count = g.Count(), Amount = g.Sum(r => r.TotalAmount) }).ToList();

            var topRooms = live.GroupBy(r => r.ProductId)
                .Select(g => new
                {
                    Name = RoomName(g.Key),
                    Count = g.Count(),
                    Amount = g.Where(r => IsRevenue(r.Status)).Sum(r => r.TotalAmount)
                })
                .OrderByDescending(x => x.Count).ThenByDescending(x => x.Amount).Take(10).ToList();

            // A few plain-language findings for the summary page
            var insights = new List<string>();
            if (byMonth.Count > 0)
            {
                var best = byMonth.OrderByDescending(m => m.Amount).First();
                insights.Add($"Best month for revenue: {best.Month:MMMM yyyy} with ₱{best.Amount:N2} from {best.Count} stay(ies).");
            }
            if (topRooms.Count > 0)
                insights.Add($"Most reserved room/service: {topRooms[0].Name} ({topRooms[0].Count} booking(s)).");
            if (live.Count > 0)
                insights.Add($"{cancelled} of {live.Count} reservations ({cancelled * 100.0 / live.Count:0.#}%) were cancelled or refunded.");
            insights.Add(openComplaints == 0
                ? "There are no open guest complaints."
                : $"{openComplaints} guest complaint(s) are still open and need follow-up.");
            if (pending > 0)
                insights.Add($"{pending} reservation(s) are still pending confirmation.");

            Document.Create(container =>
            {
                // ---------------- Cover ----------------
                CoverPage(container, "Full Performance Report", d.CompanyName,
                    new[]
                    {
                        ("Reporting period", $"{d.From:MMM d, yyyy}  to  {d.To:MMM d, yyyy}"),
                        ("Subscription plan", string.IsNullOrWhiteSpace(d.PlanName) ? "—" : d.PlanName),
                        ("Generated", DateTime.Now.ToString("MMMM d, yyyy  h:mm tt"))
                    },
                    new[]
                    {
                        "1  Executive summary", "2  Charts", "3  Reservations by status", "4  Revenue by month",
                        "5  Top rooms & services", "6  Guest feedback", "7  Staff", "8  Promotions", "9  Reservation register"
                    });

                // ---------------- Body ----------------
                container.Page(page =>
                {
                    BodyPageSetup(page, d.CompanyName, "Full Performance Report");

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        // 1. Executive summary
                        col.Item().Element(c => SectionTitle(c, "1  Executive summary"));
                        col.Item().Row(r =>
                        {
                            r.Spacing(10);
                            Kpi(r, "Confirmed revenue", $"₱{revenue:N0}", Teal);
                            Kpi(r, "Reservations", live.Count.ToString(), Navy);
                            Kpi(r, "Active guests", d.TotalCustomers.ToString(), Teal);
                        });
                        col.Item().Row(r =>
                        {
                            r.Spacing(10);
                            Kpi(r, "Pending", pending.ToString(), "#F2A33A");
                            Kpi(r, "Open complaints", openComplaints.ToString(), openComplaints > 0 ? "#D64545" : Teal);
                            Kpi(r, "Active staff", activeStaff.ToString(), Navy);
                        });

                        col.Item().PaddingTop(6).Text("Highlights").Bold().FontSize(11).FontColor(Navy);
                        foreach (var line in insights)
                            col.Item().Row(r =>
                            {
                                r.ConstantItem(14).Text("•").FontColor(Coral).Bold();
                                r.RelativeItem().Text(line);
                            });

                        // 2. Charts
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "2  Charts"));
                        ChartBlock(col, "Reservations by status", d.StatusChart);
                        ChartBlock(col, "Revenue trend (confirmed reservations)", d.RevenueChart);
                        col.Item().PageBreak();
                        ChartBlock(col, "Most reserved rooms & services", d.TopRoomsChart);
                        ChartBlock(col, "Guest feedback breakdown", d.FeedbackChart);

                        // 3. Reservations by status
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "3  Reservations by status"));
                        col.Item().Element(c => DataTable(c,
                            new[] { "Status", "Reservations", "Share", "Amount" },
                            new[] { 2f, 1.2f, 1f, 1.6f },
                            byStatus.Select(s => new[]
                            {
                                s.Status, s.Count.ToString(),
                                live.Count == 0 ? "0%" : $"{s.Count * 100.0 / live.Count:0.#}%",
                                $"₱{s.Amount:N2}"
                            }), rightFrom: 1));

                        // 4. Revenue by month
                        col.Item().Element(c => SectionTitle(c, "4  Revenue by month"));
                        col.Item().Element(c => DataTable(c,
                            new[] { "Month", "Stays", "Revenue" },
                            new[] { 2f, 1f, 1.6f },
                            byMonth.Select(m => new[] { m.Month.ToString("MMMM yyyy"), m.Count.ToString(), $"₱{m.Amount:N2}" })
                                   .Append(new[] { "Total", byMonth.Sum(m => m.Count).ToString(), $"₱{byMonth.Sum(m => m.Amount):N2}" }),
                            rightFrom: 1, boldLast: byMonth.Count > 0));

                        // 5. Top rooms
                        col.Item().Element(c => SectionTitle(c, "5  Top rooms & services"));
                        col.Item().Element(c => DataTable(c,
                            new[] { "#", "Room / service", "Bookings", "Revenue" },
                            new[] { 0.5f, 3f, 1f, 1.6f },
                            topRooms.Select((t, i) => new[] { (i + 1).ToString(), t.Name, t.Count.ToString(), $"₱{t.Amount:N2}" }),
                            rightFrom: 2));

                        // 6. Feedback
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "6  Guest feedback"));
                        col.Item().Row(r =>
                        {
                            r.Spacing(10);
                            Kpi(r, "Total feedback", d.Feedback.Count.ToString(), Navy);
                            Kpi(r, "Complaints", d.Feedback.Count(f => f.IsComplaint).ToString(), "#F2A33A");
                            Kpi(r, "Open complaints", openComplaints.ToString(), openComplaints > 0 ? "#D64545" : Teal);
                            Kpi(r, "Resolved", d.Feedback.Count(f => f.IsComplaint && f.Status == "Resolved").ToString(), Teal);
                        });
                        col.Item().Element(c => DataTable(c,
                            new[] { "Date", "Guest", "Type", "Status", "Comment" },
                            new[] { 1.2f, 1.6f, 1.1f, 1f, 4f },
                            d.Feedback.OrderByDescending(f => f.CreatedAt).Take(60).Select(f => new[]
                            {
                                f.CreatedAt.ToString("MM/dd/yyyy"), GuestName(f.CustomerId),
                                f.IsComplaint ? "Complaint" : "Feedback", f.Status, Truncate(f.Comments, 140)
                            })));
                        if (d.Feedback.Count > 60)
                            col.Item().Text($"Showing the 60 most recent of {d.Feedback.Count} feedback records.").Italic().FontColor(Muted).FontSize(8.5f);

                        // 7. Staff
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "7  Staff"));
                        col.Item().Element(c => DataTable(c,
                            new[] { "Name", "Role", "Email", "Phone", "Hired", "Status" },
                            new[] { 2f, 1f, 2.6f, 1.5f, 1.2f, 1f },
                            d.Staff.Where(s => !s.IsArchived).OrderBy(s => s.FullName).Select(s => new[]
                            {
                                s.FullName, s.Role, s.Email, s.PhoneNumber ?? "", s.HireDate.ToString("MM/dd/yyyy"),
                                s.IsActive ? "Active" : "Inactive"
                            })));

                        // 8. Promotions
                        col.Item().Element(c => SectionTitle(c, "8  Promotions"));
                        col.Item().Element(c => DataTable(c,
                            new[] { "Code", "Description", "Discount", "Eligible", "Status" },
                            new[] { 1.4f, 3.6f, 1f, 1.3f, 1f },
                            d.Promotions.Where(p => !p.IsArchived).OrderByDescending(p => p.IsActive).Select(p => new[]
                            {
                                p.Code, p.Description, $"{p.DiscountPercent:0.##}%", p.EligibleCategory,
                                p.IsActive ? "Active" : "Inactive"
                            }), rightFrom: 99));

                        // 9. Register
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "9  Reservation register"));
                        col.Item().Text("Every reservation in the reporting period.").FontColor(Muted).FontSize(8.5f);
                        col.Item().Element(c => DataTable(c,
                            new[] { "ID", "Guest", "Room / service", "Check-in", "Check-out", "Status", "Amount" },
                            new[] { 0.6f, 2f, 2f, 1.2f, 1.2f, 1.2f, 1.4f },
                            live.OrderByDescending(r => r.CheckInDate).Select(r => new[]
                            {
                                r.ReservationId.ToString(), GuestName(r.CustomerId), RoomName(r.ProductId),
                                r.CheckInDate.ToString("MM/dd/yyyy"), r.CheckOutDate.ToString("MM/dd/yyyy"),
                                r.Status, $"₱{r.TotalAmount:N2}"
                            }), rightFrom: 6));
                    });
                });
            }).GeneratePdf(filePath);
        }

        // ================================================================
        // SUPER ADMIN: PLATFORM REPORT
        // ================================================================
        public static void ExportPlatformReport(string filePath, PlatformReportData d)
        {
            var tiers = new[] { "Micro", "Small", "Medium" }
                .Select(t => new { Tier = t, Count = d.Companies.Count(c => c.SubscriptionTier == t) }).ToList();
            int activeSubs = d.Companies.Count(c => c.IsSubscriptionActive);

            Document.Create(container =>
            {
                CoverPage(container, "Platform Report", "Resort CRM — all companies",
                    new[]
                    {
                        ("Companies", d.TotalCompanies.ToString()),
                        ("Generated", DateTime.Now.ToString("MMMM d, yyyy  h:mm tt"))
                    },
                    new[] { "1  Platform summary", "2  Subscription tiers", "3  Company list", "4  Subscription history" });

                container.Page(page =>
                {
                    BodyPageSetup(page, "Resort CRM Platform", "Platform Report");

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        col.Item().Element(c => SectionTitle(c, "1  Platform summary"));
                        col.Item().Row(r =>
                        {
                            r.Spacing(10);
                            Kpi(r, "Companies", d.TotalCompanies.ToString(), Navy);
                            Kpi(r, "Active subscriptions", activeSubs.ToString(), Teal);
                            Kpi(r, "Inactive", (d.Companies.Count - activeSubs).ToString(), "#D64545");
                        });
                        col.Item().Row(r =>
                        {
                            r.Spacing(10);
                            Kpi(r, "Guests (all tenants)", d.TotalCustomers.ToString(), Teal);
                            Kpi(r, "Reservations", d.TotalReservations.ToString(), Navy);
                            Kpi(r, "Confirmed revenue", $"₱{d.TotalRevenue:N0}", Teal);
                        });
                        col.Item().Text("Only aggregate totals are included. Individual guests, reservations and staff records stay private to each company.")
                            .Italic().FontColor(Muted).FontSize(8.5f);

                        col.Item().Element(c => SectionTitle(c, "2  Subscription tiers"));
                        if (d.TierChart is not null)
                            col.Item().Image(d.TierChart).FitWidth();
                        col.Item().Element(c => DataTable(c,
                            new[] { "Tier", "Companies", "Share" },
                            new[] { 2f, 1f, 1f },
                            tiers.Select(t => new[]
                            {
                                t.Tier, t.Count.ToString(),
                                d.Companies.Count == 0 ? "0%" : $"{t.Count * 100.0 / d.Companies.Count:0.#}%"
                            }), rightFrom: 1));

                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "3  Company list"));
                        col.Item().Element(c => DataTable(c,
                            new[] { "ID", "Code", "Company", "Tier", "Subscription", "Created" },
                            new[] { 0.5f, 1.3f, 3f, 1f, 1.3f, 1.3f },
                            d.Companies.OrderBy(x => x.CompanyName).Select(x => new[]
                            {
                                x.CompanyId.ToString(), x.CompanyCode, x.CompanyName, x.SubscriptionTier,
                                x.IsSubscriptionActive ? "Active" : "Inactive", x.CreatedAt.ToString("MM/dd/yyyy")
                            })));

                        col.Item().Element(c => SectionTitle(c, "4  Subscription history"));
                        if (d.SubscriptionHistory.Count == 0)
                            col.Item().Text("No subscription records yet.").Italic().FontColor(Muted);
                        else
                            col.Item().Element(c => DataTable(c,
                                new[] { "Company", "Plan", "Start", "End", "Status" },
                                new[] { 3f, 1.2f, 1.3f, 1.3f, 1.2f },
                                d.SubscriptionHistory));
                    });
                });
            }).GeneratePdf(filePath);
        }

        // ================================================================
        // Building blocks
        // ================================================================
        private static void CoverPage(IDocumentContainer container, string title, string subject,
            (string Label, string Value)[] facts, string[] contents)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontColor(Ink));

                page.Content().Column(col =>
                {
                    col.Item().Height(430).Background(Navy).Padding(52).Column(h =>
                    {
                        h.Item().Text("RESORT CRM").FontSize(12).Bold().FontColor("#8FC3CF");
                        h.Item().PaddingTop(130).Text(title).FontSize(34).Bold().FontColor(Colors.White);
                        h.Item().PaddingTop(8).Text(subject).FontSize(20).FontColor("#FFC9B8");
                    });
                    col.Item().Height(8).Background(Coral);

                    col.Item().Padding(52).Column(m =>
                    {
                        m.Spacing(6);
                        foreach (var (label, value) in facts)
                            m.Item().Row(r =>
                            {
                                r.ConstantItem(130).Text(label).FontColor(Muted);
                                r.RelativeItem().Text(value).SemiBold();
                            });

                        m.Item().PaddingTop(18).Text("Contents").Bold().FontSize(12).FontColor(Navy);
                        foreach (var line in contents)
                            m.Item().Text(line).FontColor(Muted);
                    });
                });
            });
        }

        private static void BodyPageSetup(PageDescriptor page, string company, string reportName)
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9.5f).FontColor(Ink));

            page.Header().Column(h =>
            {
                h.Item().Row(r =>
                {
                    r.RelativeItem().Text(company).Bold().FontColor(Navy);
                    r.RelativeItem().AlignRight().Text(reportName).FontColor(Muted);
                });
                h.Item().PaddingTop(4).LineHorizontal(2).LineColor(Coral);
                h.Item().Height(10);
            });

            page.Footer().PaddingTop(8).Row(r =>
            {
                r.RelativeItem().Text($"Generated {DateTime.Now:MMM d, yyyy h:mm tt}").FontSize(8).FontColor(Muted);
                r.RelativeItem().AlignRight().Text(x =>
                {
                    x.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        }

        private static void SectionTitle(IContainer c, string text)
        {
            c.PaddingTop(6).Column(col =>
            {
                col.Item().Text(text).FontSize(14).Bold().FontColor(Navy);
                col.Item().PaddingTop(2).Width(40).LineHorizontal(3).LineColor(Teal);
            });
        }

        private static void Kpi(RowDescriptor row, string label, string value, string color)
        {
            row.RelativeItem().Background(Sand).BorderLeft(4).BorderColor(color).Padding(10).Column(c =>
            {
                c.Item().Text(label.ToUpperInvariant()).FontSize(7.5f).Bold().FontColor(Muted);
                c.Item().PaddingTop(2).Text(value).FontSize(18).Bold().FontColor(Ink);
            });
        }

        private static void ChartBlock(ColumnDescriptor col, string title, byte[]? png)
        {
            col.Item().ShowEntire().Column(c =>
            {
                c.Item().Text(title).Bold().FontSize(10.5f).FontColor(Navy);
                if (png is null)
                    c.Item().PaddingVertical(6).Text("Chart not available.").Italic().FontColor(Muted);
                else
                    c.Item().PaddingTop(4).Border(1).BorderColor(Line).Image(png).FitWidth();
            });
        }

        // Generic table: header band, zebra rows, right-aligned number columns
        private static void DataTable(IContainer container, string[] headers, float[] widths,
            IEnumerable<string[]> rows, int rightFrom = 99, bool boldLast = false)
        {
            var list = rows.ToList();
            if (list.Count == 0)
            {
                container.PaddingVertical(4).Text("No records.").Italic().FontColor(Muted);
                return;
            }

            container.Table(t =>
            {
                t.ColumnsDefinition(c => { foreach (var w in widths) c.RelativeColumn(w); });

                t.Header(h =>
                {
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = h.Cell().Background(Navy).PaddingVertical(5).PaddingHorizontal(6);
                        var text = i >= rightFrom ? cell.AlignRight() : cell;
                        text.Text(headers[i]).Bold().FontSize(8.5f).FontColor(Colors.White);
                    }
                });

                for (int r = 0; r < list.Count; r++)
                {
                    bool last = boldLast && r == list.Count - 1;
                    string bg = last ? "#E3F2F1" : (r % 2 == 0 ? Colors.White : Sand);
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var value = i < list[r].Length ? list[r][i] : "";
                        var cell = t.Cell().Background(bg).BorderBottom(1).BorderColor(Line)
                                    .PaddingVertical(4).PaddingHorizontal(6);
                        var aligned = i >= rightFrom ? cell.AlignRight() : cell;
                        var span = aligned.Text(value).FontSize(8.5f);
                        if (last) span.Bold();
                    }
                }
            });
        }

        private static string Truncate(string s, int max)
        {
            s = (s ?? "").Replace("\r", " ").Replace("\n", " ");
            return s.Length <= max ? s : s[..(max - 1)] + "…";
        }
    }
}