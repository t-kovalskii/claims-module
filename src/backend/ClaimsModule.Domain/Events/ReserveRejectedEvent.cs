using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Events;

/// <summary>FRS §14.1: RESERVE_REJECTED — a Supervisor/Manager rejected a PendingApproval reserve
/// transaction. The submitter may resubmit a revised amount per Ticket 07's resubmission flow;
/// this row stays Rejected in history. Ticket 08.</summary>
public class ReserveRejectedEvent(
    Guid claimId,
    Guid organizationId,
    Guid reserveComponentId,
    Guid reserveHistoryId,
    string componentType,
    decimal amount,
    string rejectionReason) : BaseEvent
{
    public Guid ClaimId { get; } = claimId;
    public Guid OrganizationId { get; } = organizationId;
    public Guid ReserveComponentId { get; } = reserveComponentId;
    public Guid ReserveHistoryId { get; } = reserveHistoryId;
    public string ComponentType { get; } = componentType;
    public decimal Amount { get; } = amount;
    public string RejectionReason { get; } = rejectionReason;
}
