using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ClaimsModule.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<User> Users { get; }
    DbSet<CauseOfLossCode> CauseOfLossCodes { get; }
    DbSet<ClaimStatusTransition> ClaimStatusTransitions { get; }
    DbSet<Policy> Policies { get; }
    DbSet<PolicyCoverage> PolicyCoverages { get; }
    DbSet<Claim> Claims { get; }
    DbSet<ClaimAuditLog> ClaimAuditLogs { get; }

    /// <summary>Exposed for ADR-0008's atomic claim-number counter (raw SQL) and for the
    /// explicit transaction IUnitOfWork opens around it plus the Claim aggregate insert.</summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
