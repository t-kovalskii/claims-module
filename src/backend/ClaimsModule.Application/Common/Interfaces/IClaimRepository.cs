using ClaimsModule.Domain.Entities;

namespace ClaimsModule.Application.Common.Interfaces;

/// <summary>
/// ADR-0001: the one and only persistence path for the <see cref="Claim"/> aggregate and its
/// children. Reads never go through this — they project directly off <see cref="IApplicationDbContext"/>
/// (ADR-0002).
/// </summary>
public interface IClaimRepository
{
    void Add(Claim claim);
}
