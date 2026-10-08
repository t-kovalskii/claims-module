namespace ClaimsModule.Domain.Entities;

/// <summary>FRS §5.2/§9.4: the asset types a <see cref="ClaimRiskObject"/> may describe.</summary>
public enum ClaimRiskObjectAssetType
{
    Vehicle,
    Property,
    Person,
    Equipment,
    Other
}
