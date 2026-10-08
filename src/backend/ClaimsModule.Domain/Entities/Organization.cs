using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

public class Organization : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
}
