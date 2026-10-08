using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Commands.TransitionClaimStatus;

/// <summary>
/// ADR-0001's repository path (a status transition mutates the tracked Claim aggregate) — but no
/// explicit IUnitOfWork transaction, unlike CreateClaimCommandHandler: there is exactly one
/// independently-committing operation here (the Claim update plus the audit-log row its domain
/// event adds, both inside the same SaveChangesAsync/DispatchDomainEventsInterceptor batch per
/// ADR-0003), not two that need to roll back together.
///
/// Role enforcement here is deliberately NOT a static <c>[Authorize(Roles = "...")]</c> like
/// ADR-0004 describes for reserve approval — the required role is data-driven (varies per
/// ClaimStatusTransition row), so it's enforced inside <see cref="Claim.ValidateTransition"/>
/// instead, using the authenticated caller's role claim. See Ticket 06's Comments.
/// </summary>
public class TransitionClaimStatusCommandHandler(
    IClaimRepository claimRepository,
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IDateTime dateTime)
    : IRequestHandler<TransitionClaimStatusCommand, TransitionClaimStatusResult>
{
    public async Task<TransitionClaimStatusResult> Handle(
        TransitionClaimStatusCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var actorRole = ParseActorRole();

        var transitionsFromCurrentStatus = await context.ClaimStatusTransitions
            .AsNoTracking()
            .Where(t => t.FromStatus == claim.Status)
            .ToListAsync(cancellationToken);

        var validation = claim.ValidateTransition(
            request.TargetStatus, transitionsFromCurrentStatus, actorRole, request.Reason);

        if (!validation.IsAllowed)
        {
            throw ToException(validation);
        }

        claim.ApplyTransition(request.TargetStatus, request.Reason, dateTime.UtcNow);

        // Triggers DispatchDomainEventsInterceptor (the matching StatusChanged/Closed/Reopened
        // event -> ClaimAuditLog insert) in the same SaveChanges batch (ADR-0003).
        await context.SaveChangesAsync(cancellationToken);

        return new TransitionClaimStatusResult(claim.Id, claim.Status.ToString());
    }

    private UserRole ParseActorRole()
    {
        var role = currentUserService.Role
            ?? throw new AuthenticationException("No authenticated role context.");

        return Enum.Parse<UserRole>(role);
    }

    private static BusinessRuleViolationException ToException(ClaimTransitionValidationResult validation)
    {
        var extensions = new Dictionary<string, object>();

        if (validation.ValidNextStatuses is { Count: > 0 })
        {
            extensions["validNextStatuses"] = validation.ValidNextStatuses.Select(s => s.ToString()).ToList();
        }

        if (validation.RequiredRole is not null)
        {
            extensions["requiredRole"] = validation.RequiredRole.ToString()!;
        }

        if (validation.UnmetConditions is { Count: > 0 })
        {
            extensions["unmetConditions"] = validation.UnmetConditions;
        }

        return new BusinessRuleViolationException(validation.Message!, extensions);
    }
}
