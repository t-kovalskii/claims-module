using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.5 <c>ClaimReserveComponents</c> / CONTEXT.md's <c>ReserveComponent</c>: one row per
/// component type on a <see cref="Claim"/> — a child entity, never loaded, persisted, or
/// repository-accessed independently (ADR-0001). <see cref="CurrentAmount"/> is a maintained
/// projection, recomputed from <see cref="History"/> by <see cref="RecomputeCurrentAmount"/> —
/// never set directly by application code (ADR-0007).
/// </summary>
public class ReserveComponent : AuditableEntity, IHasOrganization
{
    public Guid OrganizationId { get; set; }
    public Guid ClaimId { get; set; }
    public Claim? Claim { get; set; }

    public ReserveComponentType ComponentType { get; set; }

    /// <summary>Projection: the sum of the latest "accepted" (AutoApproved/Approved) transaction's
    /// <see cref="ReserveHistory.NewBalance"/> in <see cref="History"/> — see
    /// <see cref="RecomputeCurrentAmount"/>. Never assigned directly outside that method.</summary>
    public decimal CurrentAmount { get; set; }

    /// <summary>FRS §9.5's component-lifecycle status (Active/Closed) — distinct from the
    /// per-transaction <see cref="ReserveApprovalStatus"/> on <see cref="History"/> entries. See
    /// <see cref="ReserveComponentStatus"/>'s own remarks: this ticket never sets it to Closed.</summary>
    public ReserveComponentStatus Status { get; set; } = ReserveComponentStatus.Active;

    public string? Notes { get; set; }

    public ICollection<ReserveHistory> History { get; set; } = [];

    /// <summary>The most recently submitted transaction's approval status, or null if this
    /// component somehow has no history yet (shouldn't happen outside of a fresh in-memory
    /// instance that hasn't had its first transaction added). Pure, in-memory — operates only on
    /// the already-loaded <see cref="History"/> collection.</summary>
    public ReserveApprovalStatus? GetLatestApprovalStatus() =>
        History
            .OrderByDescending(h => h.ChangeSequence)
            .Select(h => (ReserveApprovalStatus?)h.ApprovalStatus)
            .FirstOrDefault();

    /// <summary>
    /// ADR-0007: <see cref="CurrentAmount"/> is always derived from — and consistent with —
    /// <see cref="History"/>, never independently mutated. Finds the latest (by
    /// <see cref="ReserveHistory.ChangeSequence"/>) transaction whose
    /// <see cref="ReserveHistory.ApprovalStatus"/> is AutoApproved or Approved (i.e. actually
    /// "accepted") and takes its <see cref="ReserveHistory.NewBalance"/> — PendingApproval/
    /// Rejected/Cancelled transactions never move this projection. Safe to call after any mutation
    /// to <see cref="History"/>, including ones that don't change the numeric result (e.g. a
    /// retraction), so every write path can call it unconditionally.
    /// </summary>
    public void RecomputeCurrentAmount()
    {
        var latestAccepted = History
            .Where(h => h.ApprovalStatus is ReserveApprovalStatus.AutoApproved or ReserveApprovalStatus.Approved)
            .OrderByDescending(h => h.ChangeSequence)
            .FirstOrDefault();

        CurrentAmount = latestAccepted?.NewBalance ?? 0m;
    }
}
