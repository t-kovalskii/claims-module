using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Events;

/// <summary>Raised when a <see cref="Entities.Claim"/> is first created (FNOL intake, BR-A-02).</summary>
public class ClaimCreatedEvent(Guid claimId, Guid organizationId, string claimNumber) : BaseEvent
{
    public Guid ClaimId { get; } = claimId;
    public Guid OrganizationId { get; } = organizationId;
    public string ClaimNumber { get; } = claimNumber;
}
