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
}
