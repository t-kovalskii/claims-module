namespace ClaimsModule.Domain.Entities;

/// <summary>FRS §9.3: whether a <see cref="ClaimParty"/> is a natural person or a company.</summary>
public enum ClaimPartyType
{
    Person,
    Company
}
