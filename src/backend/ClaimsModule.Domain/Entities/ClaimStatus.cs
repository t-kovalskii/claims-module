namespace ClaimsModule.Domain.Entities;

/// <summary>FRS §4.1: the full set of statuses a claim can occupy across its lifecycle.</summary>
public enum ClaimStatus
{
    Draft,
    Open,
    UnderInvestigation,
    PendingPayment,
    Closed,
    Reopened,
    Withdrawn
}
