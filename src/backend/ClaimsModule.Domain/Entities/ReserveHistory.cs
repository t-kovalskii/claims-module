using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.6 / CONTEXT.md's <c>ReserveHistory</c>: the append-only ledger of reserve transactions
/// against one <see cref="ReserveComponent"/>. <see cref="TransactionType"/>/<see cref="Amount"/>/
/// <see cref="PreviousBalance"/>/<see cref="NewBalance"/> are immutable financial facts, set once
/// at insert and never changed; <see cref="ApprovalStatus"/>/<see cref="PostingStatus"/> and their
/// accompanying who/when/why fields are workflow metadata that legitimately mutates in place on
/// this same row as it progresses (PendingApproval → Approved/Rejected/Cancelled; Pending →
/// Posted/Failed) — see <see cref="ReserveApprovalStatus"/>'s remarks for why this doesn't
/// contradict ADR-0007's "never update, only insert" framing. Deliberately not an
/// <see cref="AuditableEntity"/> (no RowVersion/soft-delete) — spec.md §Implementation Decisions
/// only names Claims/ClaimReserveComponents as RowVersion'd aggregate roots; this mirrors
/// <see cref="ClaimAuditLog"/>'s own BaseEntity-plus-manual-CreatedAt shape.
/// </summary>
public class ReserveHistory : BaseEntity, IHasOrganization
{
    public Guid OrganizationId { get; set; }
    public Guid ReserveComponentId { get; set; }
    public ReserveComponent? ReserveComponent { get; set; }

    /// <summary>FK to Claims, denormalised for query convenience (FRS §9.6).</summary>
    public Guid ClaimId { get; set; }

    public ReserveTransactionType TransactionType { get; set; }

    /// <summary>Delta amount (positive = increase, negative = decrease). Only
    /// <see cref="ReserveComponentType.SubrogationRecoverable"/> may be negative (BR-R-01).</summary>
    public decimal Amount { get; set; }

    /// <summary>The component's <see cref="ReserveComponent.CurrentAmount"/> immediately before
    /// this transaction was submitted.</summary>
    public decimal PreviousBalance { get; set; }

    /// <summary>The balance that results if this transaction's <see cref="Amount"/> is applied —
    /// always <see cref="PreviousBalance"/> + <see cref="Amount"/>, computed at insert time
    /// regardless of <see cref="ApprovalStatus"/> (so history always shows what a pending
    /// transaction's resulting balance *would* be). <see cref="ReserveComponent.CurrentAmount"/>
    /// only actually advances to this value once <see cref="ApprovalStatus"/> is AutoApproved or
    /// Approved — see <see cref="ReserveComponent.RecomputeCurrentAmount"/>.</summary>
    public decimal NewBalance { get; set; }

    public ReserveApprovalStatus ApprovalStatus { get; set; }

    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    public Guid? RejectedByUserId { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>Why the reserve was changed, as supplied by the submitter.</summary>
    public string? ChangeReason { get; set; }

    public ReservePostingStatus PostingStatus { get; set; } = ReservePostingStatus.Pending;

    /// <summary>Hangfire job ID (Ticket 08) — a string, not a GUID, matching Hangfire's own job-id
    /// shape.</summary>
    public string? PostingJobId { get; set; }

    /// <summary>ADR-0007 / FRS §6.5: <c>Reserve:{ReserveComponentId}:Change:{ChangeSequence}</c>,
    /// unique-indexed — the GL-posting job's idempotency guard (Ticket 08).</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Monotonically increasing per component, starting at 1 for its first (Add)
    /// transaction.</summary>
    public int ChangeSequence { get; set; }

    public Guid? SubmittedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
