using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

public class ClaimRepository(IApplicationDbContext context) : IClaimRepository
{
    public void Add(Claim claim) => context.Claims.Add(claim);

    public Task<Claim?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Claims
            .Include(c => c.Parties)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
}
