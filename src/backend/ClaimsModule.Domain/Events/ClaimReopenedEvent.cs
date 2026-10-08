using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Events;

/// <summary>
/// FRS §14.1: CLAIM_REOPENED — NewValue carries the reopen reason. BR-ST-04 / Ticket 06: the
/// claim's persisted <see cref="Entities.Claim.Status"/> lands on <c>Open</c> immediately, not
/// <c>Reopened</c> — see Ticket 06's Comments for the judgement call. This event still records
/// the logical Closed→Reopened transition the FRS names, for audit purposes.
/// </summary>
public class ClaimReopenedEvent(Guid claimId, Guid organizationId, string reopenReason) : BaseEvent
{
    public Guid ClaimId { get; } = claimId;
    public Guid OrganizationId { get; } = organizationId;
    public string ReopenReason { get; } = reopenReason;
}
