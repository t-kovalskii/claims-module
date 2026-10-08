using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Events;

/// <summary>Raised for every status transition except Closed/Reopened, which get their own,
/// more specific events (FRS §14.1's EventType table lists CLAIM_CLOSED/CLAIM_REOPENED
/// separately from the generic STATUS_CHANGED). Ticket 06.</summary>
public class ClaimStatusChangedEvent(
    Guid claimId,
    Guid organizationId,
    string fromStatus,
    string toStatus,
    string? reason) : BaseEvent
{
    public Guid ClaimId { get; } = claimId;
    public Guid OrganizationId { get; } = organizationId;
    public string FromStatus { get; } = fromStatus;
    public string ToStatus { get; } = toStatus;
    public string? Reason { get; } = reason;
}
