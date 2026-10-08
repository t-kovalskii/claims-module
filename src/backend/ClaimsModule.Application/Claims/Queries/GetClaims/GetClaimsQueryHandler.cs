using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Queries.GetClaims;

public class GetClaimsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetClaimsQuery, PagedResult<ClaimSummaryDto>>
{
    public async Task<PagedResult<ClaimSummaryDto>> Handle(GetClaimsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Claims.AsNoTracking();

        if (request.Status is not null)
        {
            query = query.Where(c => c.Status == request.Status);
        }

        if (request.DateFrom is not null)
        {
            var from = new DateTimeOffset(request.DateFrom.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(c => c.LossEvent.LossDate >= from);
        }

        if (request.DateTo is not null)
        {
            var to = new DateTimeOffset(request.DateTo.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            query = query.Where(c => c.LossEvent.LossDate <= to);
        }

        if (request.AssignedHandlerUserId is not null)
        {
            query = query.Where(c => c.AssignedHandlerUserId == request.AssignedHandlerUserId);
        }

        if (!string.IsNullOrWhiteSpace(request.CauseOfLossCode))
        {
            query = query.Where(c => c.LossEvent.CauseOfLossCode!.Code == request.CauseOfLossCode);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(c => c.ClaimNumber.Contains(term) || c.ClientName.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new ClaimSummaryDto(
                c.Id,
                c.ClaimNumber,
                c.ClientName,
                c.PolicyNumber,
                c.LossEvent.LossDate,
                c.Status.ToString(),
                c.LossEvent.CauseOfLossCode!.Code,
                0m)) // Reserve totals arrive with the ReserveComponent aggregate in Ticket 07.
            .ToListAsync(cancellationToken);

        return new PagedResult<ClaimSummaryDto>(items, totalCount, request.Page, request.PageSize);
    }
}
