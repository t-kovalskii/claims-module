using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Events;

/// <summary>FRS §14.1: RESERVE_AUTO_APPROVED — a submitted reserve transaction with
/// |amount| ≤ $10,000 (FRS §6.3), accepted immediately with no approval step. Ticket 07.</summary>
public class ReserveAutoApprovedEvent(
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
