using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;

public class GetClaimAuditLogQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetClaimAuditLogQuery, PagedResult<ClaimAuditLogDto>>
{
    public async Task<PagedResult<ClaimAuditLogDto>> Handle(
        GetClaimAuditLogQuery request, CancellationToken cancellationToken)
    {
        var claimExists = await context.Claims
            .AsNoTracking()
            .AnyAsync(c => c.Id == request.ClaimId, cancellationToken);

        if (!claimExists)
        {
            throw new NotFoundException(nameof(Domain.Entities.Claim), request.ClaimId);
        }

        var query = context.ClaimAuditLogs
            .AsNoTracking()
            .Where(a => a.ClaimId == request.ClaimId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new ClaimAuditLogDto(
                a.Id,
                a.EventType,
                a.Description,
                a.OldValue,
                a.NewValue,
                a.RelatedEntityId,
                a.RelatedEntityType,
                a.CreatedAt,
                a.CreatedByUserId))
            .ToListAsync(cancellationToken);

        return new PagedResult<ClaimAuditLogDto>(items, totalCount, request.Page, request.PageSize);
    }
}
