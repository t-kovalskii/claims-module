using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Events;

/// <summary>FRS §14.1: RESERVE_APPROVED — a Supervisor/Manager approved a PendingApproval reserve
/// transaction. Ticket 08.</summary>
public class ReserveApprovedEvent(
    Guid claimId,
    Guid organizationId,
    Guid reserveComponentId,
    Guid reserveHistoryId,
    string componentType,
    decimal amount,
    decimal newBalance) : BaseEvent
{
    public Guid ClaimId { get; } = claimId;
    public Guid OrganizationId { get; } = organizationId;
    public Guid ReserveComponentId { get; } = reserveComponentId;
    public Guid ReserveHistoryId { get; } = reserveHistoryId;
    public string ComponentType { get; } = componentType;
    public decimal Amount { get; } = amount;
    public decimal NewBalance { get; } = newBalance;
}
