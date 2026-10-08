using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// ADR-0008: the per-organisation, per-year counter backing atomic claim-number generation.
/// Deliberately a bare row, not an <see cref="AuditableEntity"/> — it's an internal mechanism
/// never exposed via the API and only ever touched through the atomic
/// <c>UPDATE ... OUTPUT INSERTED</c> statement in <c>ClaimNumberGenerator</c>, never via the
/// change tracker, so soft-delete/audit columns and a concurrency token would be dead weight.
/// Still implements <see cref="IHasOrganization"/> — even though every real access goes through
/// raw SQL that bypasses EF's query filters, spec.md's tenant-isolation principle is "every
/// table carries an organisation identifier [and] global query filter", and the only entities
/// exempted from it are the four reference/policy tables ADR-0009 names explicitly. This isn't
/// one of them, so it stays consistent rather than becoming a second, undocumented exception.
/// </summary>
public class ClaimNumberCounter : IHasOrganization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public int Year { get; set; }
    public int LastValue { get; set; }
}
