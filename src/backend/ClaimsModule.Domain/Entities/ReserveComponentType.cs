namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §6.2 / CONTEXT.md: the four reserve component types. Only
/// <see cref="SubrogationRecoverable"/> may hold a negative amount (BR-R-01) — every other value
/// must be strictly greater than zero.
/// </summary>
public enum ReserveComponentType
{
    Indemnity,
    Expense,
    ALAE,
    SubrogationRecoverable
}
