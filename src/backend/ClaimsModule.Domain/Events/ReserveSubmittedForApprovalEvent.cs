using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Events;

/// <summary>FRS §14.1: RESERVE_CREATED — a submitted reserve transaction with |amount| &gt;
/// $10,000, routed to PendingApproval (the specific Supervisor/Manager tier split is Ticket 08).
/// Ticket 07.</summary>
public class ReserveSubmittedForApprovalEvent(
    Guid claimId,
    Guid organizationId,
    Guid reserveComponentId,
    Guid reserveHistoryId,
    string componentType,
    decimal amount) : BaseEvent
{
    public Guid ClaimId { get; } = claimId;
    public Guid OrganizationId { get; } = organizationId;
    public Guid ReserveComponentId { get; } = reserveComponentId;
    public Guid ReserveHistoryId { get; } = reserveHistoryId;
    public string ComponentType { get; } = componentType;
    public decimal Amount { get; } = amount;
}
