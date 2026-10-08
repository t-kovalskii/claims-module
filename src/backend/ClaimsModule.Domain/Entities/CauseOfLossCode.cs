using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §5.6: standardised reference list of causes of loss, each classified under one
/// <see cref="PerilCategory"/>. Only <see cref="IsActive"/> codes are offered for selection
/// during FNOL intake. Seeded (not org-scoped) via the independently re-runnable SQL script.
/// </summary>
public class CauseOfLossCode : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PerilCategory PerilCategory { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
