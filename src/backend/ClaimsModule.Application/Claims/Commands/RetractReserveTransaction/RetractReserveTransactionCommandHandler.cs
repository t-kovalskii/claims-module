using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.RetractReserveTransaction;

/// <summary>
/// ADR-0001's repository path. The handler itself resolves <see cref="RetractReserveTransactionCommand.ReserveHistoryId"/>
/// against the loaded aggregate and maps "doesn't exist on this claim" to 404 — Claim.RetractReserveTransaction
/// is only ever asked to adjudicate genuine business-rule failures (wrong status / wrong
/// submitter), both 422, per that method's own remarks.
/// </summary>
public class RetractReserveTransactionCommandHandler(
    IClaimRepository claimRepository,
    IApplicationDbContext context,
    ICurrentUserService currentUserService)
    : IRequestHandler<RetractReserveTransactionCommand, RetractReserveTransactionResult>
{
    public async Task<RetractReserveTransactionResult> Handle(
        RetractReserveTransactionCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var history = claim.ReserveComponents
            .SelectMany(c => c.History)
            .FirstOrDefault(h => h.Id == request.ReserveHistoryId)
            ?? throw new NotFoundException(nameof(ReserveHistory), request.ReserveHistoryId);

        var actorUserId = currentUserService.UserId
            ?? throw new AuthenticationException("No authenticated user context.");

        var result = claim.RetractReserveTransaction(history, actorUserId);

        if (!result.IsAllowed)
        {
            throw new BusinessRuleViolationException(result.Message!);
        }

        // Triggers DispatchDomainEventsInterceptor (RESERVE_RETRACTED -> ClaimAuditLog insert) in
        // the same SaveChanges batch (ADR-0003).
        await context.SaveChangesAsync(cancellationToken);

        var component = result.Component!;
        return new RetractReserveTransactionResult(component.Id, history.Id, component.CurrentAmount);
    }
}
