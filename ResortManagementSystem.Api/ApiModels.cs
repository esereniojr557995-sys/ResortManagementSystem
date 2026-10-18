using ResortManagementSystem.Domain.Entities;

// Put this file in the ResortManagementSystem.Api project (global namespace).
public record CompanyOverviewRow(
    int CompanyId, string CompanyCode, string CompanyName, string SubscriptionTier, bool IsSubscriptionActive,
    DateTime? StartDate, DateTime? EndDate, decimal TotalPrice, int Months,
    string Agreement, string? AcceptedBy, DateTime? AcceptedAt);

public record NewSubscriptionRequest(string? Tier, int Months, DateTime? StartDate);
public record SetActiveRequest(bool IsActive);
public record NewTermsRequest(string? Title, string? Body);
public record AgreementStatusResponse(bool Required, Subscription Subscription, TermsAndConditions Terms);
public record AcceptTermsRequest(int SubscriptionId, int TermsId, string? AcceptedBy);