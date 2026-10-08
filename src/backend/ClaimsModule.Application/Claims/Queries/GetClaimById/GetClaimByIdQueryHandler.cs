using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Queries.GetClaimById;

public class GetClaimByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetClaimByIdQuery, ClaimDetailDto>
{
    public async Task<ClaimDetailDto> Handle(GetClaimByIdQuery request, CancellationToken cancellationToken)
    {
        var claim = await context.Claims
            .AsNoTracking()
            .Where(c => c.Id == request.Id)
            .Select(c => new ClaimDetailDto(
                c.Id,
                c.ClaimNumber,
                c.Status.ToString(),
                c.PolicyId,
                c.PolicyNumber,
                c.ClientName,
                c.AssignedHandlerUserId,
                new LossEventDto(
                    c.LossEvent.LossDate,
                    c.LossEvent.LossDescription,
                    c.LossEvent.LossLocation,
                    c.LossEvent.CauseOfLossCodeId,
                    c.LossEvent.CauseOfLossCode!.Code,
                    c.LossEvent.CauseOfLossCode!.Name,
                    c.LossEvent.EstimatedLossAmount,
                    c.LossEvent.PoliceReportNumber),
                c.Parties.Select(p => new ClaimPartyDto(
                    p.Id,
                    p.Role.ToString(),
                    p.PartyType.ToString(),
                    p.FirstName,
                    p.LastName,
                    p.CompanyName,
                    p.Email,
                    p.Phone,
                    p.Notes,
                    p.IsActive)).ToList(),
                c.RiskObjects.Select(r => new ClaimRiskObjectDto(
                    r.Id,
                    r.AssetType.ToString(),
                    r.AssetDescription,
                    r.DamageDescription,
                    r.IsPrimary,
                    r.AssetReference)).ToList(),
                new List<object>(),
                new List<object>(),
                c.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return claim ?? throw new NotFoundException(nameof(Domain.Entities.Claim), request.Id);
    }
}
