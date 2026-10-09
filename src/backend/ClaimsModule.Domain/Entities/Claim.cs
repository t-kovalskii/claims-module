using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Events;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.1 / ADR-0001: the sole aggregate root in this slice. Owns its <see cref="LossEvent"/>,
/// <see cref="ClaimParty"/> list, <see cref="ClaimRiskObject"/> list, and (Ticket 07)
/// <see cref="ReserveComponent"/> list as a single transactional and persistence boundary, loaded
/// and saved together through one ClaimRepository + Unit of Work.
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

    /// <summary>BR-R-05 / Ticket 08: a Manager has explicitly set this (by passing
    /// <c>overrideAggregateCap: true</c> on an approval that would otherwise breach it) — once
    /// set, it stays set for the life of the claim, so a later approval that would again exceed
    /// the $10,000,000 cap doesn't force the Manager to re-assert it. There is no endpoint to
    /// clear it; the FRS describes only a one-way "set the override flag" action.</summary>
    public bool AggregateCapOverrideApproved { get; set; }

    public LossEvent LossEvent { get; set; } = null!;
    public ICollection<ClaimParty> Parties { get; set; } = [];
    public ICollection<ClaimRiskObject> RiskObjects { get; set; } = [];

    /// <summary>Ticket 07 / ADR-0001 / ADR-0007: one row per component type ever opened on this
    /// claim, each carrying its own <see cref="ReserveHistory"/> ledger.</summary>
    public ICollection<ReserveComponent> ReserveComponents { get; set; } = [];

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
    /// <see cref="Parties"/>/<see cref="ReserveComponents"/> — see Ticket 06's Comments for what's
    /// deliberately still *not* checked (no persisted "validation issue" concept).
    /// </summary>
    public IReadOnlyList<string> GetUnmetConditions(ClaimStatus targetStatus, string? reason) =>
        targetStatus switch
        {
            ClaimStatus.Open => GetUnmetOpenConditions(),
            ClaimStatus.PendingPayment => GetUnmetPendingPaymentConditions(),
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

    /// <summary>FRS §4.3's four closure conditions (CC-01..04). CC-02 (no unresolved Critical
    /// issues) remains vacuously satisfied — no persisted validation-issue concept exists (see
    /// <see cref="GetUnmetOpenConditions"/>). CC-01 is now enforced (Ticket 07 extends this per
    /// Ticket 06's Comments' explicit instruction). CC-04 is a non-blocking warning (requires a
    /// justification note only when a reserve has a nonzero balance at closure) — never enforced
    /// here since it can't block the transition; the frontend's closure pre-flight checklist
    /// still surfaces it as informational.</summary>
    public IReadOnlyList<string> GetUnmetClosureConditions()
    {
        var unmet = new List<string>();

        // CC-03.
        if (!HasActiveClaimant())
        {
            unmet.Add("At least one Claimant party is required to close a claim.");
        }

        // CC-01: FRS literally reads "no reserve components with status PendingApproval remain" —
        // read functionally against this ticket's schema (component.Status is Active/Closed
        // lifecycle, not approval state) as "no component's latest transaction is still pending".
        if (HasAnyPendingApprovalReserve())
        {
            unmet.Add("No reserve components may be pending approval to close a claim.");
        }

        return unmet;
    }

    /// <summary>FRS §4.2: Open/UnderInvestigation → PendingPayment requires "at least one
    /// reserve component with status Approved". Read functionally as "at least one component
    /// has an officially-accepted balance" — AutoApproved counts exactly the same as a manually
    /// Approved one; both represent money actually set aside, just via different authority paths.
    /// Ticket 06's Comments flagged this as vacuously satisfied pending Ticket 07; now enforced.</summary>
    private List<string> GetUnmetPendingPaymentConditions()
    {
        var unmet = new List<string>();

        if (!HasAnyApprovedReserve())
        {
            unmet.Add("At least one approved reserve component is required to transition to PendingPayment.");
        }

        return unmet;
    }

    private bool HasActiveClaimant() => Parties.Any(p => p.IsActive && p.Role == ClaimPartyRole.Claimant);

    private bool HasAnyPendingApprovalReserve() =>
        ReserveComponents.Any(c => c.GetLatestApprovalStatus() == ReserveApprovalStatus.PendingApproval);

    private bool HasAnyApprovedReserve() =>
        ReserveComponents.Any(c => c.GetLatestApprovalStatus() is ReserveApprovalStatus.AutoApproved or ReserveApprovalStatus.Approved);

    /// <summary>
    /// FRS §6.1-6.4 / BR-R-01/02: opens a new reserve component (its first-ever transaction,
    /// <see cref="ReserveTransactionType.Add"/>) or submits a further transaction
    /// (<see cref="ReserveTransactionType.Adjust"/>) against an already-open one. Pure and
    /// in-memory, operating only on the already-loaded <see cref="ReserveComponents"/> — exactly
    /// the seam spec.md's Testing Decisions justify for "the combinatorial branching in reserve
    /// authority tiers". Amount-sign validation (BR-R-01) happens earlier, in
    /// SubmitReserveTransactionCommandValidator (FluentValidation/400) — by the time this method
    /// runs, <paramref name="amount"/> is already known to have a valid sign for
    /// <paramref name="componentType"/>.
    /// </summary>
    public ReserveSubmissionResult SubmitReserveTransaction(
        ReserveComponentType componentType,
        decimal amount,
        string? changeReason,
        Guid? submittedByUserId,
        DateTimeOffset now)
    {
        // BR-C-06 / FRS §8: reserve creation is blocked until a policy is linked.
        if (PolicyId is null)
        {
            return ReserveSubmissionResult.Rejected(
                "A policy must be linked to the claim before reserves can be opened.");
        }

        var component = ReserveComponents.FirstOrDefault(c => c.ComponentType == componentType);

        if (component is not null && component.GetLatestApprovalStatus() == ReserveApprovalStatus.PendingApproval)
        {
            return ReserveSubmissionResult.Rejected(
                $"{componentType} has a transaction pending approval; retract it before submitting a revised amount.");
        }

        var isFirstTransaction = component is null;
        if (component is null)
        {
            component = new ReserveComponent
            {
                OrganizationId = OrganizationId,
                ClaimId = Id,
                ComponentType = componentType,
                Status = ReserveComponentStatus.Active,
                CurrentAmount = 0m
            };
            ReserveComponents.Add(component);
        }

        var previousBalance = component.CurrentAmount;
        var changeSequence = component.History.Count + 1;
        var approvalStatus = Math.Abs(amount) <= ReserveApprovalThresholds.AutoApproveMax
            ? ReserveApprovalStatus.AutoApproved
            : ReserveApprovalStatus.PendingApproval;

        var history = new ReserveHistory
        {
            OrganizationId = OrganizationId,
            ClaimId = Id,
            ReserveComponentId = component.Id,
            ReserveComponent = component,
            TransactionType = isFirstTransaction ? ReserveTransactionType.Add : ReserveTransactionType.Adjust,
            Amount = amount,
            PreviousBalance = previousBalance,
            NewBalance = previousBalance + amount,
            ApprovalStatus = approvalStatus,
            ChangeReason = changeReason,
            PostingStatus = ReservePostingStatus.Pending,
            ChangeSequence = changeSequence,
            SubmittedByUserId = submittedByUserId,
            CreatedAt = now
        };
        history.IdempotencyKey = $"Reserve:{component.Id}:Change:{changeSequence}";

        component.History.Add(history);
        component.RecomputeCurrentAmount();

        AddDomainEvent(approvalStatus == ReserveApprovalStatus.AutoApproved
            ? new ReserveAutoApprovedEvent(Id, OrganizationId, component.Id, history.Id, componentType.ToString(), amount, history.NewBalance)
            : new ReserveSubmittedForApprovalEvent(Id, OrganizationId, component.Id, history.Id, componentType.ToString(), amount));

        return ReserveSubmissionResult.Allowed(component, history);
    }

    /// <summary>
    /// FRS §6.4's RULE: the original submitter retracts their own still-pending transaction
    /// (ApprovalStatus → Cancelled on the same row, per <see cref="ReserveApprovalStatus"/>'s
    /// remarks) so a revised amount can be submitted as a new transaction. <paramref name="history"/>
    /// must already belong to one of this claim's <see cref="ReserveComponents"/> — the handler
    /// resolves/validates existence (404 if not found on this claim) before calling this method,
    /// so every rejection here is a genuine business-rule failure (422), not a lookup failure.
    /// </summary>
    public ReserveRetractionResult RetractReserveTransaction(ReserveHistory history, Guid actorUserId)
    {
        if (history.ApprovalStatus != ReserveApprovalStatus.PendingApproval)
        {
            return ReserveRetractionResult.Rejected(
                "Only a reserve transaction pending approval can be retracted.");
        }

        if (history.SubmittedByUserId != actorUserId)
        {
            return ReserveRetractionResult.Rejected(
                "Only the original submitter may retract this reserve transaction.");
        }

        history.ApprovalStatus = ReserveApprovalStatus.Cancelled;

        var component = ReserveComponents.First(c => c.Id == history.ReserveComponentId);
        component.RecomputeCurrentAmount();

        AddDomainEvent(new ReserveRetractedEvent(
            Id, OrganizationId, component.Id, history.Id, component.ComponentType.ToString(), history.Amount));

        return ReserveRetractionResult.Allowed(component);
    }

    /// <summary>
    /// FRS §6.3/6.4, BR-R-02/03/05: a Supervisor/Manager approves a PendingApproval transaction.
    /// <paramref name="history"/> must already belong to one of this claim's
    /// <see cref="ReserveComponents"/> — the handler resolves/validates existence (404 if not
    /// found on this claim) before calling this method, same convention as
    /// <see cref="RetractReserveTransaction"/>. <paramref name="overrideAggregateCap"/> is the
    /// Manager's explicit opt-in (BR-R-05) when this approval would push the claim's total
    /// approved reserves over $10,000,000; it's only consulted when that's actually the case, and
    /// once set it sticks on <see cref="AggregateCapOverrideApproved"/> for the rest of the claim's
    /// life (no "un-set" action exists in the FRS).
    /// </summary>
    public ReserveApprovalActionResult ApproveReserveTransaction(
        ReserveHistory history, Guid actorUserId, UserRole actorRole, bool overrideAggregateCap, DateTimeOffset now)
    {
        if (history.ApprovalStatus != ReserveApprovalStatus.PendingApproval)
        {
            return ReserveApprovalActionResult.Rejected(
                "Only a reserve transaction pending approval can be approved.");
        }

        // BR-R-03: compared by userId, not role — a Manager who happens to also be the submitter
        // is still blocked, exactly like a Handler would be.
        if (history.SubmittedByUserId == actorUserId)
        {
            return ReserveApprovalActionResult.Rejected("Self-approval is not permitted.");
        }

        // BR-R-02: the threshold applies to this transaction's own amount, mirroring
        // SubmitReserveTransaction's own auto-approval check.
        var requiredRole = Math.Abs(history.Amount) > ReserveApprovalThresholds.SupervisorApproveMax
            ? UserRole.Manager
            : UserRole.Supervisor;
        if (actorRole < requiredRole)
        {
            return ReserveApprovalActionResult.Rejected(
                $"{requiredRole} approval is required for a transaction of this size.");
        }

        var component = ReserveComponents.First(c => c.Id == history.ReserveComponentId);

        // BR-R-05: the cap applies to the claim's total approved position, not this transaction
        // in isolation — project what the total would become if this approval goes through,
        // substituting this history's resulting balance for its own component's current one.
        var projectedTotal = ReserveComponents.Sum(c =>
            c.Id == component.Id ? history.NewBalance : c.CurrentAmount);

        if (projectedTotal > ReserveApprovalThresholds.AggregateApprovedCap)
        {
            if (actorRole < UserRole.Manager)
            {
                return ReserveApprovalActionResult.Rejected(
                    "Manager approval is required for a reserve that would push total approved reserves over $10,000,000.");
            }

            if (!overrideAggregateCap && !AggregateCapOverrideApproved)
            {
                return ReserveApprovalActionResult.Rejected(
                    "Total approved reserves would exceed $10,000,000. A Manager must set the override flag to approve.");
            }

            if (overrideAggregateCap)
            {
                AggregateCapOverrideApproved = true;
            }
        }

        history.ApprovalStatus = ReserveApprovalStatus.Approved;
        history.ApprovedByUserId = actorUserId;
        history.ApprovedAt = now;
        component.RecomputeCurrentAmount();

        AddDomainEvent(new ReserveApprovedEvent(
            Id, OrganizationId, component.Id, history.Id, component.ComponentType.ToString(), history.Amount, history.NewBalance));

        return ReserveApprovalActionResult.Allowed(component, history);
    }

    /// <summary>FRS §6.4: a Supervisor/Manager rejects a PendingApproval transaction with a
    /// reason (BR-R-04); the row stays Rejected in history and the submitter may submit a
    /// revised amount as a brand-new transaction — unlike retraction, a rejected row needs no
    /// separate "clear it first" step, since <see cref="SubmitReserveTransaction"/> only blocks
    /// on a component whose *latest* transaction is still PendingApproval.</summary>
    public ReserveApprovalActionResult RejectReserveTransaction(
        ReserveHistory history, Guid actorUserId, string rejectionReason, DateTimeOffset now)
    {
        if (history.ApprovalStatus != ReserveApprovalStatus.PendingApproval)
        {
            return ReserveApprovalActionResult.Rejected(
                "Only a reserve transaction pending approval can be rejected.");
        }

        history.ApprovalStatus = ReserveApprovalStatus.Rejected;
        history.RejectedByUserId = actorUserId;
        history.RejectedAt = now;
        history.RejectionReason = rejectionReason;

        var component = ReserveComponents.First(c => c.Id == history.ReserveComponentId);
        component.RecomputeCurrentAmount();

        AddDomainEvent(new ReserveRejectedEvent(
            Id, OrganizationId, component.Id, history.Id, component.ComponentType.ToString(), history.Amount, rejectionReason));

        return ReserveApprovalActionResult.Allowed(component, history);
    }
}
