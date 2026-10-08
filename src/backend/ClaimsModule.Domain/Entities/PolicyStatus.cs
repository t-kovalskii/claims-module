namespace ClaimsModule.Domain.Entities;

/// <summary>FRS §9 data model: the lifecycle status of a simulated policy.</summary>
public enum PolicyStatus
{
    Active,
    Expired,
    Cancelled
}
