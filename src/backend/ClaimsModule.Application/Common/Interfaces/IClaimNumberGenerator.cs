namespace ClaimsModule.Application.Common.Interfaces;

/// <summary>ADR-0008: atomic, per-organisation, per-year claim number generation.</summary>
public interface IClaimNumberGenerator
{
    /// <summary>Returns the next <c>CLM-{YYYY}-{7-digit-zero-padded}</c> number for the
    /// organisation, for the current UTC year. Must run inside the same transaction as the
    /// Claim insert it's generated for (see IUnitOfWork).</summary>
    Task<string> GenerateAsync(Guid organizationId, CancellationToken cancellationToken);
}
