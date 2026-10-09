namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.6's <c>ReserveHistory.ApprovalStatus</c> column — the lifecycle state of one reserve
/// transaction. Unlike the transaction's financial facts (<see cref="ReserveHistory.Amount"/>,
/// <see cref="ReserveHistory.PreviousBalance"/>, <see cref="ReserveHistory.NewBalance"/>), which
/// are set once at insert and never changed, this field legitimately mutates in place as the
/// same row progresses through its workflow (<see cref="PendingApproval"/> →
/// <see cref="Approved"/>/<see cref="Rejected"/>/<see cref="Cancelled"/>) — FRS §6.4's endpoint
/// table describes approve/reject/retract as each "sets" a status on the existing transaction,
/// never inserting a new row for the decision itself.
/// </summary>
public enum ReserveApprovalStatus
{
    /// <summary>Amount ≤ $10,000 (FRS §6.3) — accepted immediately, no human approval step.</summary>
    AutoApproved,

    /// <summary>Amount &gt; $10,000 — awaiting Supervisor/Manager approval (Ticket 08). Does not
    /// yet count toward <see cref="ReserveComponent.CurrentAmount"/>.</summary>
    PendingApproval,

    /// <summary>Manually approved by a Supervisor/Manager (Ticket 08).</summary>
    Approved,

    /// <summary>Manually rejected by a Supervisor/Manager (Ticket 08); the submitter may resubmit
    /// a revised amount as a new transaction — this row stays in history unchanged.</summary>
    Rejected,

    /// <summary>FRS §6.4's RULE: the submitter retracted their own still-pending transaction
    /// before anyone approved/rejected it (Ticket 07). Named <c>Cancelled</c>, not "Retracted",
    /// to match the FRS's own literal wording exactly — see this ticket's Comments.</summary>
    Cancelled
}
