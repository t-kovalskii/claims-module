using ClaimsModule.Domain.Entities;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.TransitionClaimStatus;

/// <summary>FRS §10.1 PUT /api/claims/{id}/status (BR-ST-01..04): transitions a claim to
/// <paramref name="TargetStatus"/> if the current status allows it, the caller's role meets the
/// transition's required permission, and the target status's own conditions (Open/Closed/
/// Reopened/Withdrawn) are satisfied. Ticket 06.</summary>
public record TransitionClaimStatusCommand(Guid ClaimId, ClaimStatus TargetStatus, string? Reason)
    : IRequest<TransitionClaimStatusResult>;

/// <summary>Deliberately minimal — the frontend re-fetches the full claim detail after a
/// successful transition (Ticket 05's Comments already call this out as the intended pattern).</summary>
public record TransitionClaimStatusResult(Guid Id, string Status);
