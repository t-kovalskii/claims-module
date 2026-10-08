using ClaimsModule.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Reference.Queries.GetCauseOfLossCodes;

public class GetCauseOfLossCodesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCauseOfLossCodesQuery, List<CauseOfLossCodeDto>>
{
    public async Task<List<CauseOfLossCodeDto>> Handle(GetCauseOfLossCodesQuery request, CancellationToken cancellationToken)
    {
        var query = context.CauseOfLossCodes
            .AsNoTracking()
            .Where(c => c.IsActive);

        if (request.PerilCategory is not null)
        {
            query = query.Where(c => c.PerilCategory == request.PerilCategory);
        }

        return await query
            .OrderBy(c => c.SortOrder)
            .Select(c => new CauseOfLossCodeDto(c.Id, c.Code, c.Name, c.PerilCategory.ToString(), c.SortOrder))
            .ToListAsync(cancellationToken);
    }
}
