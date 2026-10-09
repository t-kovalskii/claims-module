using ClaimsModule.Application.Claims.Jobs;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.ApproveReserveTransaction;

/// <summary>
/// ADR-0001's repository path. Same 404-vs-422 split as RetractReserveTransactionCommandHandler:
/// this handler resolves <see cref="ApproveReserveTransactionCommand.ReserveHistoryId"/> against
/// the loaded aggregate (404 if not found on this claim), so every rejection from
/// Claim.ApproveReserveTransaction is a genuine business-rule failure (422).
///
/// Role enforcement is two-layered, per ADR-0004: ClaimsController's
/// <c>[Authorize(Roles = "Supervisor,Manager")]</c> blocks Handlers entirely before this handler
/// ever runs; the finer Supervisor-vs-Manager split by transaction size (BR-R-02) and the
/// aggregate-cap override (BR-R-05) are both amount-dependent, so — like
/// TransitionClaimStatusCommandHandler's own status-dependent role check — they're enforced
/// inside the domain method instead of a second static attribute.
/// </summary>
public class ApproveReserveTransactionCommandHandler(
    IClaimRepository claimRepository,
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IBackgroundJobScheduler jobScheduler,
    IDateTime dateTime)
    : IRequestHandler<ApproveReserveTransactionCommand, ApproveReserveTransactionResult>
{
    public async Task<ApproveReserveTransactionResult> Handle(
        ApproveReserveTransactionCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var history = claim.ReserveComponents
            .SelectMany(c => c.History)
            .FirstOrDefault(h => h.Id == request.ReserveHistoryId)
            ?? throw new NotFoundException(nameof(ReserveHistory), request.ReserveHistoryId);

        var actorUserId = currentUserService.UserId
            ?? throw new AuthenticationException("No authenticated user context.");
        var actorRole = ParseActorRole();

        var result = claim.ApproveReserveTransaction(
            history, actorUserId, actorRole, request.OverrideAggregateCap, dateTime.UtcNow);

        if (!result.IsAllowed)
        {
            throw new BusinessRuleViolationException(result.Message!);
        }

        // Triggers DispatchDomainEventsInterceptor (RESERVE_APPROVED -> ClaimAuditLog insert) in
        // the same SaveChanges batch (ADR-0003).
        await context.SaveChangesAsync(cancellationToken);

        // FRS §6.4/§6.5: approval enqueues the GL posting simulation immediately. A second,
        // small SaveChanges stamps the returned Hangfire job id onto this same row — see
        // PostGlReserveChangeJob's remarks for why the job itself doesn't read its own id back.
        var jobId = jobScheduler.Enqueue<PostGlReserveChangeJob>(job => job.ExecuteAsync(history.Id, CancellationToken.None));
        history.PostingJobId = jobId;
        await context.SaveChangesAsync(cancellationToken);

        var component = result.Component!;
        return new ApproveReserveTransactionResult(component.Id, history.Id, history.ApprovalStatus.ToString(), component.CurrentAmount);
    }

    private UserRole ParseActorRole()
    {
        var role = currentUserService.Role
            ?? throw new AuthenticationException("No authenticated role context.");

        return Enum.Parse<UserRole>(role);
    }
}
