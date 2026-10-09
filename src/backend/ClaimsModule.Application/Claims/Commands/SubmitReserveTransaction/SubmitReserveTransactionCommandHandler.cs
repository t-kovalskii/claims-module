using ClaimsModule.Application.Claims.Jobs;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.SubmitReserveTransaction;

/// <summary>
/// ADR-0001's repository path (mutates the tracked Claim aggregate's ReserveComponents). No
/// explicit IUnitOfWork transaction — same reasoning as TransitionClaimStatusCommandHandler:
/// exactly one independently-committing operation (the reserve insert plus the matching audit-log
/// row its domain event adds, both inside the same SaveChangesAsync/DispatchDomainEventsInterceptor
/// batch per ADR-0003).
/// </summary>
public class SubmitReserveTransactionCommandHandler(
    IClaimRepository claimRepository,
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IBackgroundJobScheduler jobScheduler,
    IDateTime dateTime)
    : IRequestHandler<SubmitReserveTransactionCommand, SubmitReserveTransactionResult>
{
    public async Task<SubmitReserveTransactionResult> Handle(
        SubmitReserveTransactionCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var result = claim.SubmitReserveTransaction(
            request.ComponentType,
            request.Amount,
            request.ChangeReason,
            currentUserService.UserId,
            dateTime.UtcNow);

        if (!result.IsAllowed)
        {
            throw new BusinessRuleViolationException(result.Message!);
        }

        var component = result.Component!;
        var history = result.History!;

        // EF Core's DetectChanges, on discovering a *new* entity reachable only via navigation
        // fixup from an already-tracked Unchanged parent (claim, loaded whole by the repository),
        // can't tell "new entity with a client-generated key" apart from "this FK references an
        // already-existing row" — it defaults to assuming the latter and marks it Unchanged (then
        // Modified, since every property differs from the implicit blank baseline), producing a
        // spurious UPDATE statement that affects 0 rows and throws DbUpdateConcurrencyException.
        // This never bit Tickets 04/06 because their new child entities were always reachable from
        // a Claim that was itself freshly `Add()`ed (which correctly cascades Added to the whole
        // graph) — this is the first command to add a new child onto an *existing*, already-loaded
        // Claim. Explicitly `Add()`-ing the genuinely new entities here forces the correct state.
        context.ReserveHistory.Add(history);
        if (history.TransactionType == ReserveTransactionType.Add)
        {
            context.ReserveComponents.Add(component);
        }

        // Triggers DispatchDomainEventsInterceptor (RESERVE_CREATED/RESERVE_AUTO_APPROVED ->
        // ClaimAuditLog insert) in the same SaveChanges batch (ADR-0003).
        await context.SaveChangesAsync(cancellationToken);

        // FRS §6.3/§6.5: an auto-approved transaction is approved immediately at submission, so
        // it enqueues the GL posting job right here — a manually-approved one gets the same
        // treatment from ApproveReserveTransactionCommandHandler instead.
        if (history.ApprovalStatus == ReserveApprovalStatus.AutoApproved)
        {
            var jobId = jobScheduler.Enqueue<PostGlReserveChangeJob>(job => job.ExecuteAsync(history.Id, CancellationToken.None));
            history.PostingJobId = jobId;
            await context.SaveChangesAsync(cancellationToken);
        }

        return new SubmitReserveTransactionResult(
            component.Id,
            history.Id,
            component.ComponentType.ToString(),
            history.TransactionType.ToString(),
            history.Amount,
            history.PreviousBalance,
            history.NewBalance,
            history.ApprovalStatus.ToString(),
            component.CurrentAmount,
            history.ChangeSequence);
    }
}
