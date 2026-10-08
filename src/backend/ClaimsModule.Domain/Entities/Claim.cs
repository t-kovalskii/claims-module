using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Events;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.1 / ADR-0001: the sole aggregate root in this slice. Owns its <see cref="LossEvent"/>,
/// <see cref="ClaimParty"/> list, and <see cref="ClaimRiskObject"/> list as a single
/// transactional and persistence boundary, loaded and saved together through one
/// ClaimRepository + Unit of Work. Reserve components (ADR-0001, ADR-0007) arrive in Ticket 07
/// — not modelled here yet.
/// </summary>
public class Claim : AuditableEntity, IHasOrganization
{
    public Guid OrganizationId { get; set; }

    /// <summary>ADR-0008: <c>CLM-{YYYY}-{7-digit-zero-padded}</c>, unique per organisation.</summary>
    public string ClaimNumber { get; set; } = string.Empty;

    public Guid? PolicyId { get; set; }

    /// <summary>Denormalised from the linked <c>Policy</c> at creation time (FRS §9.1) — the
    /// Policy/Claim boundary is intentionally not a live join (ADR-0009).</summary>
    public string? PolicyNumber { get; set; }

    /// <summary>Denormalised from the linked Policy's client name, or — when no policy is
    /// linked — derived from the first Claimant party at intake (a judgement call; see Ticket
    /// 04's Comments).</summary>
    public string ClientName { get; set; } = string.Empty;

    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;
    public Guid? AssignedHandlerUserId { get; set; }

    /// <summary>FRS §9.1 / Ticket 06: set when <see cref="Status"/> transitions to
    /// <see cref="ClaimStatus.Closed"/>; never otherwise.</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>FRS §9.1 / Ticket 06: the reason/justification note supplied on the Closed
    /// transition, if any.</summary>
    public string? ClosureReason { get; set; }

    public LossEvent LossEvent { get; set; } = null!;
    public ICollection<ClaimParty> Parties { get; set; } = [];
    public ICollection<ClaimRiskObject> RiskObjects { get; set; } = [];

    // EF Core materialises via this parameterless constructor; application code must go
    // through Create to guarantee every claim starts Draft with its ClaimCreated event raised.
    private Claim()
    {
    }

    public static Claim Create(
        Guid organizationId,
        string claimNumber,
        Guid? policyId,
        string? policyNumber,
        string clientName,
        Guid? assignedHandlerUserId,
        LossEvent lossEvent,
        IEnumerable<ClaimParty> parties,
        IEnumerable<ClaimRiskObject> riskObjects)
    {
        var claim = new Claim
        {
            OrganizationId = organizationId,
            ClaimNumber = claimNumber,
            PolicyId = policyId,
            PolicyNumber = policyNumber,
            ClientName = clientName,
            Status = ClaimStatus.Draft,
            AssignedHandlerUserId = assignedHandlerUserId,
            LossEvent = lossEvent
        };

        foreach (var party in parties)
        {
            claim.Parties.Add(party);
        }

        foreach (var riskObject in riskObjects)
        {
            claim.RiskObjects.Add(riskObject);
        }

        claim.AddDomainEvent(new ClaimCreatedEvent(claim.Id, organizationId, claimNumber));

        return claim;
    }

    /// <summary>
    /// BR-ST-01: validates a requested status transition against the caller-supplied subset of
    /// <see cref="ClaimStatusTransition"/> rows whose <see cref="ClaimStatusTransition.FromStatus"/>
    /// equals this claim's current <see cref="Status"/> (the handler loads exactly that subset —
    /// this method never queries anything itself, which is what lets it be exercised by the
    /// narrow in-memory unit-test seam spec.md's Testing Decisions call out for "the status
    /// transition state machine" branching).
    /// </summary>
    public ClaimTransitionValidationResult ValidateTransition(
        ClaimStatus targetStatus,
        IReadOnlyCollection<ClaimStatusTransition> transitionsFromCurrentStatus,
        UserRole actorRole,
        string? reason)
    {
        var rule = transitionsFromCurrentStatus.FirstOrDefault(t => t.ToStatus == targetStatus);
        if (rule is null)
        {
            var validNextStatuses = transitionsFromCurrentStatus.Select(t => t.ToStatus).Distinct().ToList();
            return ClaimTransitionValidationResult.InvalidTransition(
                $"Transition from {Status} to {targetStatus} is not permitted.",
                validNextStatuses);
        }

        // RequiredPermission is the *minimum* role; UserRole's declared order (Handler < Supervisor
        // < Manager) is the authority hierarchy itself (FRS §3: each role inherits the one below),
        // so an ordinal comparison is the whole check — no separate rank table needed.
        if (actorRole < rule.RequiredPermission)
        {
            return ClaimTransitionValidationResult.InsufficientRole(
                "Your role does not have authority to perform this transition.",
                rule.RequiredPermission);
        }

        var unmetConditions = GetUnmetConditions(targetStatus, reason);
        if (unmetConditions.Count > 0)
        {
            return ClaimTransitionValidationResult.UnmetBusinessConditions(
                $"Claim cannot transition to {targetStatus}.",
                unmetConditions);
        }

        return ClaimTransitionValidationResult.Allowed();
    }

    /// <summary>
    /// Mutates <see cref="Status"/> (and, for Closed, <see cref="ClosedAt"/>/<see cref="ClosureReason"/>)
    /// and raises the matching domain event. Callers must have already confirmed
    /// <see cref="ValidateTransition"/> returned <c>Allowed</c> — this method does not re-check.
    /// </summary>
    public void ApplyTransition(ClaimStatus targetStatus, string? reason, DateTimeOffset now)
    {
        var previousStatus = Status;

        switch (targetStatus)
        {
            case ClaimStatus.Reopened:
                // BR-ST-04: Reopened→Open happens "immediately" per the FRS's own transition
                // table — there is no separately-authorised second step, so this command lands
                // the claim straight on Open rather than persisting the transient Reopened value.
                // See Ticket 06's Comments for the full judgement call.
                Status = ClaimStatus.Open;
                AddDomainEvent(new ClaimReopenedEvent(Id, OrganizationId, reason!));
                break;

            case ClaimStatus.Closed:
                Status = ClaimStatus.Closed;
                ClosedAt = now;
                ClosureReason = reason;
                AddDomainEvent(new ClaimClosedEvent(Id, OrganizationId, previousStatus.ToString(), reason));
                break;

            default:
                Status = targetStatus;
                AddDomainEvent(new ClaimStatusChangedEvent(
                    Id, OrganizationId, previousStatus.ToString(), targetStatus.ToString(), reason));
                break;
        }
    }

    /// <summary>
    /// BR-ST-02/03/04 branching: the per-target-status business conditions beyond "is this
    /// from/to pair and role listed". Pure and in-memory, operating only on already-loaded
    /// <see cref="Parties"/> — see Ticket 06's Comments for what's deliberately *not* checked
    /// yet (no persisted "validation issue" concept; no ReserveComponents until Ticket 07/08).
    /// </summary>
    public IReadOnlyList<string> GetUnmetConditions(ClaimStatus targetStatus, string? reason) =>
        targetStatus switch
        {
            ClaimStatus.Open => GetUnmetOpenConditions(),
            ClaimStatus.Closed => GetUnmetClosureConditions(),
            ClaimStatus.Reopened => string.IsNullOrWhiteSpace(reason)
                ? ["A reopen reason is required."]
                : [],
            ClaimStatus.Withdrawn => string.IsNullOrWhiteSpace(reason)
                ? ["A withdrawal reason is required."]
                : [],
            _ => []
        };

    private List<string> GetUnmetOpenConditions()
    {
        var unmet = new List<string>();

        // BR-ST-02(b) / BR-C-03.
        if (!HasActiveClaimant())
        {
            unmet.Add("At least one Claimant party is required to open a claim.");
        }

        // BR-ST-02(a) "no Critical validation issues on the claim": every Critical rule
        // (BR-C-01/05/07) already blocks claim creation itself — ValidationBehaviour throws
        // before a Claim is ever persisted — and Ticket 04 didn't introduce a separate,
        // persisted "validation issue" entity. No Draft claim can exist today with an
        // outstanding Critical issue, so this condition is vacuously satisfied. See Ticket 06's
        // Comments.

        return unmet;
    }

    /// <summary>FRS §4.3's four closure conditions (CC-01..04). CC-01 (no PendingApproval
    /// reserves) and CC-02 (no unresolved Critical issues) are vacuously satisfied today — this
    /// aggregate has no <c>ReserveComponents</c> yet (Ticket 07/08) and no persisted
    /// validation-issue concept exists (see <see cref="GetUnmetOpenConditions"/>). Ticket 07/08
    /// MUST extend this method once ReserveComponents exist, or claims become closable with
    /// reserves still pending approval. CC-04 is a non-blocking warning (requires a
    /// justification note only when a reserve has a nonzero balance at closure) — never enforced
    /// here since it can't block the transition, and moot today with no reserves.</summary>
    public IReadOnlyList<string> GetUnmetClosureConditions()
    {
        var unmet = new List<string>();

        // CC-03.
        if (!HasActiveClaimant())
        {
            unmet.Add("At least one Claimant party is required to close a claim.");
        }

        return unmet;
    }

    private bool HasActiveClaimant() => Parties.Any(p => p.IsActive && p.Role == ClaimPartyRole.Claimant);
}
