using Microsoft.EntityFrameworkCore;
using ResortManagementSystem.Domain.Entities;
using ResortManagementSystem.Infrastructure.Data;

// Put this file in the ResortManagementSystem.Api project (global namespace, so Program.cs needs no extra using).
public static class SubscriptionHelper
{
    // Monthly price per tier. Adjust to your real pricing.
    public static readonly Dictionary<string, decimal> PlanPrices = new()
    {
        ["Micro"] = 999m,
        ["Small"] = 2499m,
        ["Medium"] = 4999m
    };

    // Returns the company's Active subscription; creates a 1-month starter record if none exists.
    public static async Task<Subscription> GetOrCreateCurrentAsync(MasterResortDbContext db, Company company)
    {
        var current = await db.Subscriptions
            .Where(s => s.CompanyId == company.CompanyId && s.Status == "Active")
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
        if (current is not null) return current;

        var tier = PlanPrices.ContainsKey(company.SubscriptionTier) ? company.SubscriptionTier : "Micro";
        var price = PlanPrices[tier];
        var start = DateTime.UtcNow.Date;

        current = new Subscription
        {
            CompanyId = company.CompanyId,
            Tier = tier,
            PricePerMonth = price,
            Months = 1,
            TotalPrice = price,
            StartDate = start,
            EndDate = start.AddMonths(1),
            Status = "Active"
        };
        db.Subscriptions.Add(current);
        await db.SaveChangesAsync();
        return current;
    }

    // Returns the current terms; seeds version 1 if the table is empty.
    public static async Task<TermsAndConditions> EnsureTermsAsync(MasterResortDbContext db)
    {
        var current = await db.TermsVersions
            .Where(t => t.IsCurrent)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync();
        if (current is not null) return current;

        var latest = await db.TermsVersions.OrderByDescending(t => t.Version).FirstOrDefaultAsync();
        if (latest is not null)
        {
            latest.IsCurrent = true;
            await db.SaveChangesAsync();
            return latest;
        }

        var terms = new TermsAndConditions
        {
            Version = 1,
            Title = "Subscription Terms and Conditions",
            Body = "By subscribing you agree to pay the plan fee for the chosen period, to keep your account " +
                   "credentials secure, and to use the system only for your resort's operations. Access may be " +
                   "suspended if the subscription lapses. Replace this text from the Super Admin terms editor.",
            IsCurrent = true
        };
        db.TermsVersions.Add(terms);
        await db.SaveChangesAsync();
        return terms;
    }
}