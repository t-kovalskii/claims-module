namespace ClaimsModule.Domain.Common;

/// <summary>
/// Base for entities needing audit columns, soft delete, and row-version concurrency —
/// the data model conventions every later entity is expected to adopt.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>
/// Marks an entity as scoped to one organisation, enforced via an EF Core global query filter.
/// </summary>
public interface IHasOrganization
{
    Guid OrganizationId { get; set; }
}
