namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.6's <c>ReserveHistory.TransactionType</c> column. Ticket 07 only ever produces
/// <see cref="Add"/> (the component's first-ever transaction) or <see cref="Adjust"/> (every
/// subsequent one, regardless of whether earlier attempts were retracted/rejected — see this
/// ticket's Comments for the exact rule). <see cref="Reverse"/> is defined for FRS/ADR-0007
/// schema completeness but no command in this ticket produces one.
/// </summary>
public enum ReserveTransactionType
{
    Add,
    Adjust,
    Reverse
}
