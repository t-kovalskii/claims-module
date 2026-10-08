using ClaimsModule.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Policies.Queries.SearchPolicies;

public class SearchPoliciesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<SearchPoliciesQuery, List<PolicySummaryDto>>
{
    public async Task<List<PolicySummaryDto>> Handle(SearchPoliciesQuery request, CancellationToken cancellationToken)
    {
        var query = context.Policies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var term = request.Q.Trim();
            query = query.Where(p => p.PolicyNumber.Contains(term) || p.ClientName.Contains(term));
        }

        return await query
            .OrderBy(p => p.PolicyNumber)
            .Select(p => new PolicySummaryDto(
                p.Id,
                p.PolicyNumber,
                p.ClientName,
                p.EffectiveDate,
                p.ExpirationDate,
                p.Status.ToString(),
                p.Coverages.Select(c => c.CoverageType.ToString()).ToList()))
            .ToListAsync(cancellationToken);
    }
}
