using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.RejectReserveTransaction;

/// <summary>ADR-0001's repository path. Same 404-vs-422 split as
/// RetractReserveTransactionCommandHandler/ApproveReserveTransactionCommandHandler. No GL posting
/// job here — FRS §6.4: approval enqueues it, rejection never does.</summary>
public class RejectReserveTransactionCommandHandler(
    IClaimRepository claimRepository,
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IDateTime dateTime)
    : IRequestHandler<RejectReserveTransactionCommand, RejectReserveTransactionResult>
{
    public async Task<RejectReserveTransactionResult> Handle(
        RejectReserveTransactionCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var history = claim.ReserveComponents
            .SelectMany(c => c.History)
            .FirstOrDefault(h => h.Id == request.ReserveHistoryId)
            ?? throw new NotFoundException(nameof(ReserveHistory), request.ReserveHistoryId);

        var actorUserId = currentUserService.UserId
            ?? throw new AuthenticationException("No authenticated user context.");

        var result = claim.RejectReserveTransaction(history, actorUserId, request.RejectionReason, dateTime.UtcNow);

        if (!result.IsAllowed)
        {
            throw new BusinessRuleViolationException(result.Message!);
        }

        // Triggers DispatchDomainEventsInterceptor (RESERVE_REJECTED -> ClaimAuditLog insert) in
        // the same SaveChanges batch (ADR-0003).
        await context.SaveChangesAsync(cancellationToken);

        return new RejectReserveTransactionResult(result.Component!.Id, history.Id, history.ApprovalStatus.ToString());
    }
}
