using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.4: an insured asset referenced by a <see cref="Claim"/> as having sustained damage —
/// a child entity, never loaded or persisted independently (ADR-0001).
/// </summary>
public class ClaimRiskObject : AuditableEntity, IHasOrganization
{
    public Guid OrganizationId { get; set; }
    public Guid ClaimId { get; set; }
    public Claim? Claim { get; set; }

    public ClaimRiskObjectAssetType AssetType { get; set; }
    public string AssetDescription { get; set; } = string.Empty;
    public string? DamageDescription { get; set; }
    public bool IsPrimary { get; set; }
    public string? AssetReference { get; set; }
}
