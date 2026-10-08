using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Events;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.1 / ADR-0001: the sole aggregate root in this slice. Owns its <see cref="LossEvent"/>,
/// <see cref="ClaimParty"/> list, and <see cref="ClaimRiskObject"/> list as a single
/// transactional and persistence boundary, loaded and saved together through one
/// ClaimRepository + Unit of Work. Reserve components (ADR-0001, ADR-0007) arrive in Ticket 07
/// — not modelled here yet.
/// </summary>
public class Claim : AuditableEntity, IHasOrganization
{
    public Guid OrganizationId { get; set; }

    /// <summary>ADR-0008: <c>CLM-{YYYY}-{7-digit-zero-padded}</c>, unique per organisation.</summary>
    public string ClaimNumber { get; set; } = string.Empty;

    public Guid? PolicyId { get; set; }

    /// <summary>Denormalised from the linked <c>Policy</c> at creation time (FRS §9.1) — the
    /// Policy/Claim boundary is intentionally not a live join (ADR-0009).</summary>
    public string? PolicyNumber { get; set; }

    /// <summary>Denormalised from the linked Policy's client name, or — when no policy is
    /// linked — derived from the first Claimant party at intake (a judgement call; see Ticket
    /// 04's Comments).</summary>
    public string ClientName { get; set; } = string.Empty;

    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;
    public Guid? AssignedHandlerUserId { get; set; }

    public LossEvent LossEvent { get; set; } = null!;
    public ICollection<ClaimParty> Parties { get; set; } = [];
    public ICollection<ClaimRiskObject> RiskObjects { get; set; } = [];

    // EF Core materialises via this parameterless constructor; application code must go
    // through Create to guarantee every claim starts Draft with its ClaimCreated event raised.
    private Claim()
    {
    }

    public static Claim Create(
        Guid organizationId,
        string claimNumber,
        Guid? policyId,
        string? policyNumber,
        string clientName,
        Guid? assignedHandlerUserId,
        LossEvent lossEvent,
        IEnumerable<ClaimParty> parties,
        IEnumerable<ClaimRiskObject> riskObjects)
    {
        var claim = new Claim
        {
            OrganizationId = organizationId,
            ClaimNumber = claimNumber,
            PolicyId = policyId,
            PolicyNumber = policyNumber,
            ClientName = clientName,
            Status = ClaimStatus.Draft,
            AssignedHandlerUserId = assignedHandlerUserId,
            LossEvent = lossEvent
        };

        foreach (var party in parties)
        {
            claim.Parties.Add(party);
        }

        foreach (var riskObject in riskObjects)
        {
            claim.RiskObjects.Add(riskObject);
        }

        claim.AddDomainEvent(new ClaimCreatedEvent(claim.Id, organizationId, claimNumber));

        return claim;
    }
}
