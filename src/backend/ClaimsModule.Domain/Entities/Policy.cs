using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §5.5: a simulated policy FNOL intake can link a claim to. This is not the aggregate
/// root boundary described in ADR-0001 (that's <c>Claim</c> only) — policies are static,
/// seeded simulation data, never mutated by application commands. Seeded (not org-scoped) via
/// the independently re-runnable SQL script, including the FRS's own expired-policy example
/// (POL-2023-000099) used later to exercise the loss-date-outside-effective-period warning.
/// </summary>
public class Policy : AuditableEntity
{
    public string PolicyNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public PolicyStatus Status { get; set; }
    public ICollection<PolicyCoverage> Coverages { get; set; } = [];
}
