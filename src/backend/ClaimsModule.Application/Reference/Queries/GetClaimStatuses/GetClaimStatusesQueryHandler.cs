using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Reference.Queries.GetClaimStatuses;

public class GetClaimStatusesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetClaimStatusesQuery, List<ClaimStatusDto>>
{
    public async Task<List<ClaimStatusDto>> Handle(GetClaimStatusesQuery request, CancellationToken cancellationToken)
    {
        var transitions = await context.ClaimStatusTransitions
            .AsNoTracking()
            .Select(t => new { t.FromStatus, t.ToStatus, t.RequiredPermission })
            .ToListAsync(cancellationToken);

        // Every status appears in the response even when it has no outgoing transitions
        // (e.g. Withdrawn, a terminal state per FRS §4.2) — the reference set is the enum,
        // not just the rows that happen to exist in the transitions table.
        return Enum.GetValues<ClaimStatus>()
            .Select(status => new ClaimStatusDto(
                status.ToString(),
                transitions
                    .Where(t => t.FromStatus == status)
                    .Select(t => new ClaimStatusTransitionDto(t.ToStatus.ToString(), t.RequiredPermission.ToString()))
                    .ToList()))
            .ToList();
    }
}
