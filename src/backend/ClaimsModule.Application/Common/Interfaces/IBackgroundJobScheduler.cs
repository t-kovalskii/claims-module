using System.Linq.Expressions;

namespace ClaimsModule.Application.Common.Interfaces;

/// <summary>
/// Ticket 08: the one seam the Application layer uses to enqueue fire-and-forget background work
/// (FRS §12.1's <c>PostGlReserveChangeJob</c>, enqueued via Hangfire's own
/// <c>BackgroundJob.Enqueue</c>). Kept as an Application-owned abstraction — implemented against
/// Hangfire in <c>ClaimsModule.Infrastructure</c> — so command handlers and their tests don't take
/// a direct Hangfire dependency, mirroring <c>SlaBreachScanJob</c>'s own "Hangfire-agnostic" job
/// classes one layer further out.
/// </summary>
public interface IBackgroundJobScheduler
{
    /// <summary>Enqueues <paramref name="methodCall"/> for fire-and-forget execution and returns
    /// the scheduler's own job id (Hangfire's), so the caller can stamp it onto whatever row the
    /// job concerns (e.g. <c>ReserveHistory.PostingJobId</c>).</summary>
    string Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall);
}
