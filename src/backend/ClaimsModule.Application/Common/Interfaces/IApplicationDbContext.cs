using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<User> Users { get; }
    DbSet<CauseOfLossCode> CauseOfLossCodes { get; }
    DbSet<ClaimStatusTransition> ClaimStatusTransitions { get; }
    DbSet<Policy> Policies { get; }
    DbSet<PolicyCoverage> PolicyCoverages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
