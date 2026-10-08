using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.3: a claimant, witness, or other party associated with a <see cref="Claim"/> — a
/// child entity, never loaded or persisted independently (ADR-0001).
/// </summary>
public class ClaimParty : AuditableEntity, IHasOrganization
{
    public Guid OrganizationId { get; set; }
    public Guid ClaimId { get; set; }
    public Claim? Claim { get; set; }

    public ClaimPartyRole Role { get; set; }
    public ClaimPartyType PartyType { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }

    /// <summary>FRS §9.3: soft-remove from the claim without deleting the row (BR-A-01-adjacent —
    /// parties stay visible in history). Distinct from AuditableEntity.IsDeleted, which this
    /// slice never sets for parties.</summary>
    public bool IsActive { get; set; } = true;
}
