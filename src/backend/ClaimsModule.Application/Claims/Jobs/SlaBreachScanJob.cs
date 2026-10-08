using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Jobs;

/// <summary>
/// Ticket 09 / FRS: the recurring SLA breach scan. Finds claims stuck in <see
/// cref="ClaimStatus.Draft"/> or <see cref="ClaimStatus.Open"/> with no update in the last 48+
/// hours and writes one <see cref="AuditEventTypes.SlaBreachDetected"/> audit entry per claim,
/// throttled to at most one new entry per claim per rolling 24-hour window.
///
/// Deliberately audit-only: this method never assigns to <c>Claim.Status</c> and queries claims
/// with <c>AsNoTracking()</c>, so there is no tracked change for <c>SaveChangesAsync</c> to
/// persist back to the <c>Claims</c> table under any circumstance.
///
/// Hangfire-agnostic by design (per the ticket): this class has no Hangfire dependency at all —
/// only <see cref="IApplicationDbContext"/> and <see cref="IDateTime"/> — so a test (or
/// Program.cs's <c>RecurringJob.AddOrUpdate</c> registration) can invoke <see
/// cref="ExecuteAsync"/> directly without waiting on or faking Hangfire's own scheduler.
/// </summary>
public class SlaBreachScanJob(IApplicationDbContext context, IDateTime dateTime)
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromHours(48);
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromHours(24);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = dateTime.UtcNow;
        var staleCutoff = utcNow - StaleThreshold;
        var throttleCutoff = utcNow - ThrottleWindow;

        // LastModifiedAt is only stamped by AuditableEntitySaveChangesInterceptor once a claim
        // is actually modified post-creation (Ticket 01) — a never-touched claim still has it
        // null, so "last touched" falls back to CreatedAt.
        var staleClaims = await context.Claims
            .AsNoTracking()
            .Where(c => c.Status == ClaimStatus.Draft || c.Status == ClaimStatus.Open)
            .Where(c => (c.LastModifiedAt ?? c.CreatedAt) <= staleCutoff)
            .Select(c => new { c.Id, c.OrganizationId })
            .ToListAsync(cancellationToken);

        if (staleClaims.Count == 0)
        {
            return;
        }

        var recentlyFlaggedClaimIds = await context.ClaimAuditLogs
            .AsNoTracking()
            .Where(a => a.EventType == AuditEventTypes.SlaBreachDetected && a.CreatedAt > throttleCutoff)
            .Select(a => a.ClaimId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var recentlyFlagged = recentlyFlaggedClaimIds.ToHashSet();

        foreach (var claim in staleClaims)
        {
            if (recentlyFlagged.Contains(claim.Id))
            {
                continue;
            }

            context.ClaimAuditLogs.Add(new ClaimAuditLog
            {
                ClaimId = claim.Id,
                OrganizationId = claim.OrganizationId,
                EventType = AuditEventTypes.SlaBreachDetected,
                Description = "Claim has had no update in 48+ hours while in Draft or Open status.",
                CreatedAt = utcNow
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
