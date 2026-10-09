using ClaimsModule.Application.Claims.Queries.GetClaimDocuments;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
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
                c.ReserveComponents.Select(rc => new ReserveComponentDto(
                    rc.Id,
                    rc.ComponentType.ToString(),
                    rc.CurrentAmount,
                    rc.Status.ToString(),
                    rc.History
                        .OrderByDescending(h => h.ChangeSequence)
                        .Select(h => (string?)h.ApprovalStatus.ToString())
                        .FirstOrDefault(),
                    rc.History
                        .Where(h => h.ApprovalStatus == ReserveApprovalStatus.PendingApproval)
                        .OrderByDescending(h => h.ChangeSequence)
                        .Select(h => (decimal?)h.Amount)
                        .FirstOrDefault(),
                    rc.History
                        .OrderByDescending(h => h.ChangeSequence)
                        .Select(h => new ReserveHistoryDto(
                            h.Id,
                            h.TransactionType.ToString(),
                            h.Amount,
                            h.PreviousBalance,
                            h.NewBalance,
                            h.ApprovalStatus.ToString(),
                            h.ChangeReason,
                            h.PostingStatus.ToString(),
                            h.SubmittedByUserId,
                            h.ApprovedByUserId,
                            h.ApprovedAt,
                            h.RejectedByUserId,
                            h.RejectedAt,
                            h.RejectionReason,
                            h.ChangeSequence,
                            h.CreatedAt))
                        .ToList())).ToList(),
                new List<ClaimDocumentDto>(),
                c.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (claim is null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Claim), request.Id);
        }

        // Ticket 10: a second round trip rather than a nested projection — ClaimDocument has no
        // navigation back from Claim (it's not part of the aggregate; see its own remarks), so
        // there's nothing for the single Select above to join against.
        var documents = await context.GetForClaimAsync(request.Id, cancellationToken);

        return claim with { Documents = documents };
    }
}
