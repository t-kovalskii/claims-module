namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.6's <c>ReserveHistory.PostingStatus</c> column — tracks the (Ticket 08) GL-posting
/// Hangfire job for this transaction. Every row this ticket creates stays <see cref="Pending"/>;
/// no command here enqueues or runs that job.
/// </summary>
public enum ReservePostingStatus
{
    Pending,
    Posted,
    Failed,
    Cancelled
}
