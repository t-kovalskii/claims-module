namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §6.3's authority thresholds, applied to the amount of the individual transaction (never
/// the running total). Ticket 07 only consumes <see cref="AutoApproveMax"/> (routing ≤ $10,000 to
/// <see cref="ReserveApprovalStatus.AutoApproved"/>, anything above to
/// <see cref="ReserveApprovalStatus.PendingApproval"/>) — <see cref="SupervisorApproveMax"/> is
/// defined here now so Ticket 08's Supervisor-vs-Manager tier split has a single, shared home for
/// both constants rather than inventing its own.
/// </summary>
public static class ReserveApprovalThresholds
{
    /// <summary>Amounts with an absolute value ≤ this are auto-approved.</summary>
    public const decimal AutoApproveMax = 10_000m;

    /// <summary>Amounts with an absolute value above this require Manager approval (Ticket 08);
    /// between <see cref="AutoApproveMax"/> and this require Supervisor or Manager.</summary>
    public const decimal SupervisorApproveMax = 100_000m;

    /// <summary>BR-R-05: the aggregate cap across all of a claim's approved reserves (Ticket 08).</summary>
    public const decimal AggregateApprovedCap = 10_000_000m;
}
