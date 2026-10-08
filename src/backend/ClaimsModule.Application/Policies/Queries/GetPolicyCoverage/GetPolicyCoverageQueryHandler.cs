using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Policies.Queries.GetPolicyCoverage;

public class GetPolicyCoverageQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPolicyCoverageQuery, List<PolicyCoverageDto>>
{
    public async Task<List<PolicyCoverageDto>> Handle(GetPolicyCoverageQuery request, CancellationToken cancellationToken)
    {
        // Single round trip: project the policy's coverages (possibly empty) alongside its own
        // existence, so a FirstOrDefaultAsync-returns-null means "no such policy" (same shape as
        // GetCurrentUserQueryHandler), not conflated with "a policy with zero coverages".
        var coverages = await context.Policies
            .AsNoTracking()
            .Where(p => p.Id == request.PolicyId)
            .Select(p => p.Coverages
                .Select(c => new PolicyCoverageDto(c.Id, c.CoverageType.ToString()))
                .ToList())
            .FirstOrDefaultAsync(cancellationToken);

        return coverages ?? throw new NotFoundException(nameof(Domain.Entities.Policy), request.PolicyId);
    }
}
