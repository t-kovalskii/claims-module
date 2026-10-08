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

    /// <summary>Ticket 06: the tracked-load path for commands that mutate an existing Claim
    /// aggregate (status transitions) — includes <see cref="Claim.Parties"/> since the
    /// transition state machine's Open/Closed conditions need them. Returns null rather than
    /// throwing; callers map that to <see cref="Exceptions.NotFoundException"/>.</summary>
    Task<Claim?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
