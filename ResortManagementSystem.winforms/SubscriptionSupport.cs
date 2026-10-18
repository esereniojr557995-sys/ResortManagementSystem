using System.Net.Http.Json;
using ResortManagementSystem.Domain.Entities;

namespace ResortManagementSystem.Winforms
{
    /// <summary>Response of GET /companies/{id}/agreement-status</summary>
    public class AgreementStatusDto
    {
        public bool Required { get; set; }
        public Subscription Subscription { get; set; } = new();
        public TermsAndConditions Terms { get; set; } = new();
    }

    /// <summary>One row of GET /admin/companies/overview</summary>
    public class CompanyOverviewDto
    {
        public int CompanyId { get; set; }
        public string CompanyCode { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string SubscriptionTier { get; set; } = "Micro";
        public bool IsSubscriptionActive { get; set; } = true;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal TotalPrice { get; set; }
        public int Months { get; set; }
        public string Agreement { get; set; } = "None";
        public string? AcceptedBy { get; set; }
        public DateTime? AcceptedAt { get; set; }
    }

    internal static class SubscriptionFormat
    {
        // The API stores UTC; show local time
        public static string LocalStamp(DateTime utc) =>
            DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("MMM d, yyyy h:mm tt");

        public static string PlanEnds(DateTime? end)
        {
            if (end is null) return "-";
            var date = end.Value.ToString("MMM d, yyyy");
            var days = (end.Value.Date - DateTime.Today).Days;
            if (days < 0) return $"{date} (expired)";
            if (days == 0) return $"{date} (today)";
            return $"{date} ({days} day{(days == 1 ? "" : "s")} left)";
        }
    }

    /// <summary>Runs after login: makes a company Admin accept the current terms before continuing.</summary>
    internal static class TermsGate
    {
        private static readonly HttpClient Http = new() { BaseAddress = new Uri("http://localhost:5226") };

        // true = the user may continue (nothing to accept, already accepted, or just accepted)
        public static bool EnsureAccepted(int companyId, string acceptedBy)
        {
            if (companyId <= 0) return true;

            AgreementStatusDto? status;
            try
            {
                // Run on a thread-pool thread: no message loop is running yet at this point
                status = Task.Run(() =>
                    Http.GetFromJsonAsync<AgreementStatusDto>($"/companies/{companyId}/agreement-status"))
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not check the subscription terms:\n{ex.Message}",
                    "Terms check failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (status is null || !status.Required) return true;

            using var form = new TermsAgreementForm(Http, companyId, acceptedBy, status);
            return form.ShowDialog() == DialogResult.OK;
        }
    }
}