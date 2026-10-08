using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>A coverage type available on a <see cref="Policy"/> (FRS §5.5 / §9).</summary>
public class PolicyCoverage : AuditableEntity
{
    public Guid PolicyId { get; set; }
    public Policy? Policy { get; set; }
    public CoverageType CoverageType { get; set; }
}
