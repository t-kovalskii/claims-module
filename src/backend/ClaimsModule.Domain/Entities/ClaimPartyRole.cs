namespace ClaimsModule.Domain.Entities;

/// <summary>FRS §7.5 / BR-P-02: the five party roles a claim may record.</summary>
public enum ClaimPartyRole
{
    Claimant,
    Insured,
    ThirdParty,
    Witness,
    Attorney
}
