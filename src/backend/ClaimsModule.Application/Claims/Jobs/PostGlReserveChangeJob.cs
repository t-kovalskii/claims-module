using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Jobs;

/// <summary>
/// FRS §6.5/§12.1's <c>PostGLReserveChangeJob</c>: simulates posting an approved reserve change
/// to a general ledger. Enqueued fire-and-forget (via <see cref="IBackgroundJobScheduler"/>)
/// immediately after a reserve transaction is approved — auto-approved at submission time
/// (<c>SubmitReserveTransactionCommandHandler</c>) or manually approved
/// (<c>ApproveReserveTransactionCommandHandler</c>).
///
/// Re-entrancy (BR-R-06): checks <see cref="ReserveHistory.PostingStatus"/> before doing
/// anything — a row already <see cref="ReservePostingStatus.Posted"/> is a no-op. The
/// <see cref="ReserveHistory.IdempotencyKey"/>'s own unique index (ADR-0007) guards against two
/// *different* history rows ever colliding on the same key; this check is what makes running the
/// *same* row's job twice produce exactly one audit entry and one status update.
///
/// Hangfire-agnostic by design, same rationale as <c>SlaBreachScanJob</c>: only
/// <see cref="IApplicationDbContext"/> and <see cref="IDateTime"/>, so a test can invoke
/// <see cref="ExecuteAsync"/> directly without waiting on Hangfire's own scheduler. The Hangfire
/// job id itself (FRS: "update ReserveHistory.PostingJobId with Hangfire job ID") is stamped by
/// the enqueuing command handler instead, right after <c>IBackgroundJobScheduler.Enqueue</c>
/// returns it — the one place that actually has it, without this class needing a Hangfire-typed
/// parameter (e.g. <c>PerformContext</c>) just to read its own id back.
/// </summary>
public class PostGlReserveChangeJob(IApplicationDbContext context, IDateTime dateTime)
{
    public async Task ExecuteAsync(Guid reserveHistoryId, CancellationToken cancellationToken = default)
    {
        var history = await context.ReserveHistory
            .FirstOrDefaultAsync(h => h.Id == reserveHistoryId, cancellationToken);

        if (history is null || history.PostingStatus == ReservePostingStatus.Posted)
        {
            return;
        }

        context.ClaimAuditLogs.Add(new ClaimAuditLog
        {
            ClaimId = history.ClaimId,
            OrganizationId = history.OrganizationId,
            EventType = AuditEventTypes.GlPostingSimulated,
            Description = $"Simulated GL posting: DR Change in Outstanding Reserves / CR Outstanding Loss Reserves, Amount = {history.Amount:C}.",
            RelatedEntityId = history.Id,
            RelatedEntityType = nameof(ReserveHistory),
            CreatedAt = dateTime.UtcNow
        });

        history.PostingStatus = ReservePostingStatus.Posted;

        await context.SaveChangesAsync(cancellationToken);
    }
}
