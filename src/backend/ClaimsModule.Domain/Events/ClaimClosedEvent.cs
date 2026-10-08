using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Events;

/// <summary>FRS §14.1: CLAIM_CLOSED — NewValue carries the closure reason/note, not the new
/// status (the status is always Closed by construction). Ticket 06.</summary>
public class ClaimClosedEvent(Guid claimId, Guid organizationId, string fromStatus, string? closureReason) : BaseEvent
{
    public Guid ClaimId { get; } = claimId;
    public Guid OrganizationId { get; } = organizationId;
    public string FromStatus { get; } = fromStatus;
    public string? ClosureReason { get; } = closureReason;
}
