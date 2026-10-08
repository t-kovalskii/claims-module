using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.8 / BR-A-01: an immutable, append-only event log entry for a claim. Deliberately not
/// an <see cref="AuditableEntity"/> — it has no soft-delete or row-version concurrency because
/// the application layer must never update or delete these rows, only insert them.
/// </summary>
public class ClaimAuditLog : BaseEntity, IHasOrganization
{
    public Guid OrganizationId { get; set; }
    public Guid ClaimId { get; set; }

    /// <summary>One of <see cref="AuditEventTypes"/>, e.g. <c>CLAIM_CREATED</c>.</summary>
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
