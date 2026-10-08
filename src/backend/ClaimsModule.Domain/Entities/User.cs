using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// A seeded login identity. ADR-0004: identity-proofing is mocked (no password check),
/// but the role and organisation claims issued from this record are real and server-trusted.
/// </summary>
public class User : AuditableEntity, IHasOrganization
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}
