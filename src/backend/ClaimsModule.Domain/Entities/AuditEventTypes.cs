namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.8's example <see cref="ClaimAuditLog.EventType"/> values. Stored as a plain string
/// column (not an enum) so later tickets can add new event types without touching this one.
/// Only the values this ticket actually writes are defined here; later tickets add their own.
/// </summary>
public static class AuditEventTypes
{
    public const string ClaimCreated = "CLAIM_CREATED";

    /// <summary>Ticket 09: written by the SLA monitoring job — audit-only, never a status
    /// change (see <c>SlaBreachScanJob</c>).</summary>
    public const string SlaBreachDetected = "SLA_BREACH_DETECTED";

    /// <summary>Ticket 06: every successful status transition except Closed/Reopened, which get
    /// their own, more specific event types below.</summary>
    public const string StatusChanged = "STATUS_CHANGED";

    /// <summary>Ticket 06: a claim transitioning to Closed.</summary>
    public const string ClaimClosed = "CLAIM_CLOSED";

    /// <summary>Ticket 06: a claim transitioning Closed→Reopened (BR-ST-04) — the claim's
    /// persisted status lands on Open immediately; see Ticket 06's Comments.</summary>
    public const string ClaimReopened = "CLAIM_REOPENED";

    /// <summary>Ticket 07: a reserve transaction submitted and routed to PendingApproval
    /// (amount &gt; $10,000). <see cref="ReserveAutoApproved"/> is used instead when the amount
    /// auto-approves.</summary>
    public const string ReserveCreated = "RESERVE_CREATED";

    /// <summary>Ticket 07: a reserve transaction auto-approved immediately (amount ≤ $10,000).</summary>
    public const string ReserveAutoApproved = "RESERVE_AUTO_APPROVED";

    /// <summary>Ticket 07: the submitter retracted their own pending reserve transaction.</summary>
    public const string ReserveRetracted = "RESERVE_RETRACTED";

    /// <summary>Ticket 08: a Supervisor/Manager approved a PendingApproval reserve transaction.</summary>
    public const string ReserveApproved = "RESERVE_APPROVED";

    /// <summary>Ticket 08: a Supervisor/Manager rejected a PendingApproval reserve transaction.</summary>
    public const string ReserveRejected = "RESERVE_REJECTED";

    /// <summary>Ticket 08 / FRS §6.5: written by <c>PostGlReserveChangeJob</c> once it has
    /// simulated the GL posting for an approved reserve change.</summary>
    public const string GlPostingSimulated = "GL_POSTING_SIMULATED";

    /// <summary>Ticket 08 / FRS §12.1: written when the GL posting job exhausts Hangfire's
    /// retries without succeeding.</summary>
    public const string GlPostingFailed = "GL_POSTING_FAILED";

    /// <summary>Ticket 10: a document was uploaded and attached to the claim.</summary>
    public const string DocumentUploaded = "DOCUMENT_UPLOADED";
}
