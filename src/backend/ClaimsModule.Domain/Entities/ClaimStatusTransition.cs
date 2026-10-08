using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §4.2 / BR-ST-01: one row of the claim status state machine. Any transition not present
/// in this table is invalid. <see cref="RequiredPermission"/> is the minimum <see cref="UserRole"/>
/// able to perform the transition (Supervisor/Manager inherit everything a Handler can do, per
/// FRS §3's role capability table) — derived from FRS §4.2's "Rules / Conditions" column, which
/// names "Handler" explicitly for the routine transitions and reserves Closed/Reopened for the
/// Supervisor-gated "manage claim status transitions" capability (BR-ST-04 is explicit for Reopened).
/// Seeded (not org-scoped) via the independently re-runnable SQL script; enforced by Ticket 06.
/// </summary>
public class ClaimStatusTransition : AuditableEntity
{
    public ClaimStatus FromStatus { get; set; }
    public ClaimStatus ToStatus { get; set; }
    public UserRole RequiredPermission { get; set; }
}
