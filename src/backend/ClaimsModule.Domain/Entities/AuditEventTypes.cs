namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.8's example <see cref="ClaimAuditLog.EventType"/> values. Stored as a plain string
/// column (not an enum) so later tickets can add new event types without touching this one.
/// Only the values this ticket actually writes are defined here; later tickets add their own.
/// </summary>
public static class AuditEventTypes
{
    public const string ClaimCreated = "CLAIM_CREATED";
}
