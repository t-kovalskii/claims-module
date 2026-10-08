using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.2: the loss incident that gave rise to a <see cref="Claim"/> — a 1:1 child entity,
/// never loaded or persisted independently (ADR-0001).
/// </summary>
public class LossEvent : AuditableEntity, IHasOrganization
{
    public Guid OrganizationId { get; set; }
    public Guid ClaimId { get; set; }
    public Claim? Claim { get; set; }

    public DateTimeOffset LossDate { get; set; }
    public string LossDescription { get; set; } = string.Empty;
    public string? LossLocation { get; set; }
    public Guid CauseOfLossCodeId { get; set; }
    public CauseOfLossCode? CauseOfLossCode { get; set; }
    public decimal? EstimatedLossAmount { get; set; }
    public string? PoliceReportNumber { get; set; }
}
