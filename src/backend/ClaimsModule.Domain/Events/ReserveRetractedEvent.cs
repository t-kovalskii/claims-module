using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Events;

/// <summary>FRS §14.1: RESERVE_RETRACTED — the submitter retracted their own pending reserve
/// transaction before anyone approved/rejected it. Ticket 07.</summary>
public class ReserveRetractedEvent(
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
