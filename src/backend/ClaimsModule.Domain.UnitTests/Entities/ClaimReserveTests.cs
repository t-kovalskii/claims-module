using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Events;

namespace ClaimsModule.Domain.UnitTests.Entities;

/// <summary>
/// Ticket 07 / spec.md's narrow "Claim only" unit-test seam: the reserve authority-tier routing
/// (≤ $10,000 auto-approve vs. above pending-approval), amount-sign branching's interaction with
/// the aggregate, and the "pending reserve must be retracted before resubmission" rule are
/// exercised here purely in-memory, with no HTTP or database.
/// </summary>
public class ClaimReserveTests
{
    private static Claim NewClaimWithPolicy(Guid? policyId = null)
    {
        var claim = Claim.Create(
            organizationId: Guid.NewGuid(),
            claimNumber: "CLM-2026-0000001",
            policyId: policyId ?? Guid.NewGuid(),
            policyNumber: "POL-1",
            clientName: "Jane Doe",
            assignedHandlerUserId: null,
            lossEvent: new LossEvent
            {
                LossDate = DateTimeOffset.UtcNow.AddDays(-1),
                LossDescription = "A sufficiently long loss description for the aggregate.",
                CauseOfLossCodeId = Guid.NewGuid()
            },
            parties: [],
            riskObjects: []);

        claim.ClearDomainEvents();
        return claim;
    }

    [Fact]
    public void SubmitReserveTransaction_is_blocked_when_no_policy_is_linked()
    {
        var claim = NewClaimWithPolicy(policyId: null);
        claim.PolicyId = null;

        var result = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 5000m, "Initial reserve", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.False(result.IsAllowed);
        Assert.Equal("A policy must be linked to the claim before reserves can be opened.", result.Message);
    }

    [Fact]
    public void SubmitReserveTransaction_at_or_below_10000_is_AutoApproved_and_updates_CurrentAmount()
    {
        var claim = NewClaimWithPolicy();

        var result = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 10_000m, "Initial reserve", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.True(result.IsAllowed);
        Assert.Equal(ReserveApprovalStatus.AutoApproved, result.History!.ApprovalStatus);
        Assert.Equal(10_000m, result.Component!.CurrentAmount);
        Assert.Equal(ReserveTransactionType.Add, result.History.TransactionType);
        var @event = Assert.Single(claim.DomainEvents);
        Assert.IsType<ReserveAutoApprovedEvent>(@event);
    }

    [Fact]
    public void SubmitReserveTransaction_above_10000_is_PendingApproval_and_does_not_move_CurrentAmount()
    {
        var claim = NewClaimWithPolicy();

        var result = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 10_000.01m, "Initial reserve", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.True(result.IsAllowed);
        Assert.Equal(ReserveApprovalStatus.PendingApproval, result.History!.ApprovalStatus);
        Assert.Equal(0m, result.Component!.CurrentAmount);
        var @event = Assert.Single(claim.DomainEvents);
        Assert.IsType<ReserveSubmittedForApprovalEvent>(@event);
    }

    [Fact]
    public void SubmitReserveTransaction_the_first_time_for_a_component_is_TransactionType_Add()
    {
        var claim = NewClaimWithPolicy();

        var result = claim.SubmitReserveTransaction(ReserveComponentType.Expense, 1000m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(ReserveTransactionType.Add, result.History!.TransactionType);
        Assert.Equal(1, result.History.ChangeSequence);
        Assert.Equal(0m, result.History.PreviousBalance);
        Assert.Equal(1000m, result.History.NewBalance);
    }

    [Fact]
    public void SubmitReserveTransaction_against_an_already_open_AutoApproved_component_is_TransactionType_Adjust()
    {
        var claim = NewClaimWithPolicy();
        var submitter = Guid.NewGuid();
        claim.SubmitReserveTransaction(ReserveComponentType.Expense, 1000m, null, submitter, DateTimeOffset.UtcNow);

        var result = claim.SubmitReserveTransaction(ReserveComponentType.Expense, 500m, "Top-up", submitter, DateTimeOffset.UtcNow);

        Assert.True(result.IsAllowed);
        Assert.Equal(ReserveTransactionType.Adjust, result.History!.TransactionType);
        Assert.Equal(2, result.History.ChangeSequence);
        Assert.Equal(1000m, result.History.PreviousBalance);
        Assert.Equal(1500m, result.History.NewBalance);
        Assert.Equal(1500m, result.Component!.CurrentAmount);
    }

    [Fact]
    public void SubmitReserveTransaction_is_rejected_while_the_component_has_a_transaction_pending_approval()
    {
        var claim = NewClaimWithPolicy();
        claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 50_000m, "Big reserve", Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 20_000m, "Revised amount", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.False(result.IsAllowed);
        Assert.Contains("retract it before submitting a revised amount", result.Message);
    }

    [Fact]
    public void SubmitReserveTransaction_two_different_component_types_do_not_interfere()
    {
        var claim = NewClaimWithPolicy();

        claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 5000m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var subrogation = claim.SubmitReserveTransaction(ReserveComponentType.SubrogationRecoverable, -8000m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.True(subrogation.IsAllowed);
        Assert.Equal(2, claim.ReserveComponents.Count);
        Assert.Equal(-8000m, subrogation.Component!.CurrentAmount);
    }

    [Fact]
    public void RetractReserveTransaction_rejects_a_transaction_that_is_not_PendingApproval()
    {
        var claim = NewClaimWithPolicy();
        var submitter = Guid.NewGuid();
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 5000m, null, submitter, DateTimeOffset.UtcNow);

        var result = claim.RetractReserveTransaction(submission.History!, submitter);

        Assert.False(result.IsAllowed);
        Assert.Equal("Only a reserve transaction pending approval can be retracted.", result.Message);
    }

    [Fact]
    public void RetractReserveTransaction_rejects_a_user_who_is_not_the_original_submitter()
    {
        var claim = NewClaimWithPolicy();
        var submitter = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 50_000m, null, submitter, DateTimeOffset.UtcNow);

        var result = claim.RetractReserveTransaction(submission.History!, someoneElse);

        Assert.False(result.IsAllowed);
        Assert.Equal("Only the original submitter may retract this reserve transaction.", result.Message);
    }

    [Fact]
    public void RetractReserveTransaction_by_the_original_submitter_cancels_it_and_raises_ReserveRetractedEvent()
    {
        var claim = NewClaimWithPolicy();
        var submitter = Guid.NewGuid();
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 50_000m, null, submitter, DateTimeOffset.UtcNow);
        claim.ClearDomainEvents();

        var result = claim.RetractReserveTransaction(submission.History!, submitter);

        Assert.True(result.IsAllowed);
        Assert.Equal(ReserveApprovalStatus.Cancelled, submission.History!.ApprovalStatus);
        Assert.Equal(0m, result.Component!.CurrentAmount);
        var @event = Assert.Single(claim.DomainEvents);
        Assert.IsType<ReserveRetractedEvent>(@event);
    }

    [Fact]
    public void After_retraction_a_revised_amount_can_be_resubmitted_as_a_new_transaction()
    {
        var claim = NewClaimWithPolicy();
        var submitter = Guid.NewGuid();
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 50_000m, null, submitter, DateTimeOffset.UtcNow);
        claim.RetractReserveTransaction(submission.History!, submitter);

        var resubmission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 8_000m, "Revised, smaller amount", submitter, DateTimeOffset.UtcNow);

        Assert.True(resubmission.IsAllowed);
        Assert.Equal(ReserveApprovalStatus.AutoApproved, resubmission.History!.ApprovalStatus);
        Assert.Equal(8_000m, resubmission.Component!.CurrentAmount);
        // The original retracted record remains in history, untouched.
        Assert.Equal(2, resubmission.Component.History.Count);
        Assert.Contains(resubmission.Component.History, h => h.Id == submission.History!.Id && h.ApprovalStatus == ReserveApprovalStatus.Cancelled);
    }

    [Fact]
    public void GetUnmetClosureConditions_blocks_Closed_while_a_reserve_component_is_PendingApproval()
    {
        var claim = NewClaimWithPolicy();
        claim.Parties.Add(new ClaimParty { Role = ClaimPartyRole.Claimant, PartyType = ClaimPartyType.Person, FirstName = "Jane", LastName = "Doe", IsActive = true });
        claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 50_000m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var unmet = claim.GetUnmetClosureConditions();

        Assert.Contains("No reserve components may be pending approval to close a claim.", unmet);
    }

    [Fact]
    public void GetUnmetClosureConditions_allows_Closed_when_the_only_reserve_is_AutoApproved()
    {
        var claim = NewClaimWithPolicy();
        claim.Parties.Add(new ClaimParty { Role = ClaimPartyRole.Claimant, PartyType = ClaimPartyType.Person, FirstName = "Jane", LastName = "Doe", IsActive = true });
        claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 5000m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var unmet = claim.GetUnmetClosureConditions();

        Assert.Empty(unmet);
    }

    [Fact]
    public void ValidateTransition_to_PendingPayment_is_blocked_without_any_approved_reserve()
    {
        var claim = NewClaimWithPolicy();
        claim.Status = ClaimStatus.Open;
        var allowed = new[] { new ClaimStatusTransition { FromStatus = ClaimStatus.Open, ToStatus = ClaimStatus.PendingPayment, RequiredPermission = UserRole.Handler } };

        var result = claim.ValidateTransition(ClaimStatus.PendingPayment, allowed, UserRole.Handler, null);

        Assert.False(result.IsAllowed);
        Assert.Contains("At least one approved reserve component is required to transition to PendingPayment.", result.UnmetConditions!);
    }

    [Fact]
    public void ValidateTransition_to_PendingPayment_succeeds_with_an_AutoApproved_reserve()
    {
        var claim = NewClaimWithPolicy();
        claim.Status = ClaimStatus.Open;
        claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 5000m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var allowed = new[] { new ClaimStatusTransition { FromStatus = ClaimStatus.Open, ToStatus = ClaimStatus.PendingPayment, RequiredPermission = UserRole.Handler } };

        var result = claim.ValidateTransition(ClaimStatus.PendingPayment, allowed, UserRole.Handler, null);

        Assert.True(result.IsAllowed);
    }

    // --- Ticket 08: approval workflow (authority tiers, self-approval, aggregate cap) ---

    [Fact]
    public void ApproveReserveTransaction_rejects_a_transaction_that_is_not_PendingApproval()
    {
        var claim = NewClaimWithPolicy();
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 5000m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = claim.ApproveReserveTransaction(submission.History!, Guid.NewGuid(), UserRole.Manager, false, DateTimeOffset.UtcNow);

        Assert.False(result.IsAllowed);
        Assert.Equal("Only a reserve transaction pending approval can be approved.", result.Message);
    }

    [Fact]
    public void ApproveReserveTransaction_rejects_self_approval()
    {
        var claim = NewClaimWithPolicy();
        var submitter = Guid.NewGuid();
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 50_000m, null, submitter, DateTimeOffset.UtcNow);

        var result = claim.ApproveReserveTransaction(submission.History!, submitter, UserRole.Manager, false, DateTimeOffset.UtcNow);

        Assert.False(result.IsAllowed);
        Assert.Equal("Self-approval is not permitted.", result.Message);
    }

    [Fact]
    public void ApproveReserveTransaction_rejects_a_Supervisor_approving_above_100000()
    {
        var claim = NewClaimWithPolicy();
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 150_000m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = claim.ApproveReserveTransaction(submission.History!, Guid.NewGuid(), UserRole.Supervisor, false, DateTimeOffset.UtcNow);

        Assert.False(result.IsAllowed);
        Assert.Contains("Manager approval is required", result.Message);
    }

    [Fact]
    public void ApproveReserveTransaction_allows_a_Supervisor_approving_between_10000_and_100000()
    {
        var claim = NewClaimWithPolicy();
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 50_000m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        claim.ClearDomainEvents();

        var result = claim.ApproveReserveTransaction(submission.History!, Guid.NewGuid(), UserRole.Supervisor, false, DateTimeOffset.UtcNow);

        Assert.True(result.IsAllowed);
        Assert.Equal(ReserveApprovalStatus.Approved, submission.History!.ApprovalStatus);
        Assert.Equal(50_000m, result.Component!.CurrentAmount);
        var @event = Assert.Single(claim.DomainEvents);
        Assert.IsType<ReserveApprovedEvent>(@event);
    }

    [Fact]
    public void ApproveReserveTransaction_allows_a_Manager_approving_above_100000()
    {
        var claim = NewClaimWithPolicy();
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 150_000m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = claim.ApproveReserveTransaction(submission.History!, Guid.NewGuid(), UserRole.Manager, false, DateTimeOffset.UtcNow);

        Assert.True(result.IsAllowed);
        Assert.Equal(150_000m, result.Component!.CurrentAmount);
    }

    [Fact]
    public void ApproveReserveTransaction_stamps_ApprovedByUserId_and_ApprovedAt()
    {
        var claim = NewClaimWithPolicy();
        var approver = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 50_000m, null, Guid.NewGuid(), now);

        claim.ApproveReserveTransaction(submission.History!, approver, UserRole.Supervisor, false, now);

        Assert.Equal(approver, submission.History!.ApprovedByUserId);
        Assert.Equal(now, submission.History.ApprovedAt);
    }

    [Fact]
    public void ApproveReserveTransaction_blocks_a_Manager_over_the_aggregate_cap_without_an_override()
    {
        var claim = NewClaimWithPolicy();
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 10_000_000.01m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = claim.ApproveReserveTransaction(submission.History!, Guid.NewGuid(), UserRole.Manager, false, DateTimeOffset.UtcNow);

        Assert.False(result.IsAllowed);
        Assert.Contains("$10,000,000", result.Message);
        Assert.False(claim.AggregateCapOverrideApproved);
    }

    [Fact]
    public void ApproveReserveTransaction_blocks_a_Supervisor_over_the_aggregate_cap_even_within_their_own_amount_tier()
    {
        // $50,000 alone is within Supervisor's own authority tier, but it's submitted against a
        // claim that's already sitting at $9,999,999 of approved reserves — pushing the total
        // over $10M requires Manager approval regardless of this transaction's own size.
        var claim = NewClaimWithPolicy();
        ApproveAsManager(claim, ReserveComponentType.Indemnity, 9_999_999m);
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Expense, 50_000m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = claim.ApproveReserveTransaction(submission.History!, Guid.NewGuid(), UserRole.Supervisor, false, DateTimeOffset.UtcNow);

        Assert.False(result.IsAllowed);
        Assert.Contains("Manager approval is required", result.Message);
    }

    [Fact]
    public void ApproveReserveTransaction_with_an_override_succeeds_over_the_aggregate_cap_and_sets_the_flag()
    {
        var claim = NewClaimWithPolicy();
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 10_000_000.01m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = claim.ApproveReserveTransaction(submission.History!, Guid.NewGuid(), UserRole.Manager, true, DateTimeOffset.UtcNow);

        Assert.True(result.IsAllowed);
        Assert.True(claim.AggregateCapOverrideApproved);
    }

    [Fact]
    public void ApproveReserveTransaction_does_not_require_the_override_flag_again_once_already_set()
    {
        var claim = NewClaimWithPolicy();
        ApproveAsManager(claim, ReserveComponentType.Indemnity, 10_000_000.01m, overrideAggregateCap: true);
        var second = claim.SubmitReserveTransaction(ReserveComponentType.Expense, 50_000m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = claim.ApproveReserveTransaction(second.History!, Guid.NewGuid(), UserRole.Manager, false, DateTimeOffset.UtcNow);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void RejectReserveTransaction_rejects_a_transaction_that_is_not_PendingApproval()
    {
        var claim = NewClaimWithPolicy();
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 5000m, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = claim.RejectReserveTransaction(submission.History!, Guid.NewGuid(), "Not needed", DateTimeOffset.UtcNow);

        Assert.False(result.IsAllowed);
        Assert.Equal("Only a reserve transaction pending approval can be rejected.", result.Message);
    }

    [Fact]
    public void RejectReserveTransaction_sets_Rejected_and_stamps_the_reason_by_and_at()
    {
        var claim = NewClaimWithPolicy();
        var rejector = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 50_000m, null, Guid.NewGuid(), now);
        claim.ClearDomainEvents();

        var result = claim.RejectReserveTransaction(submission.History!, rejector, "Amount not substantiated", now);

        Assert.True(result.IsAllowed);
        Assert.Equal(ReserveApprovalStatus.Rejected, submission.History!.ApprovalStatus);
        Assert.Equal(rejector, submission.History.RejectedByUserId);
        Assert.Equal(now, submission.History.RejectedAt);
        Assert.Equal("Amount not substantiated", submission.History.RejectionReason);
        Assert.Equal(0m, result.Component!.CurrentAmount);
        var @event = Assert.Single(claim.DomainEvents);
        Assert.IsType<ReserveRejectedEvent>(@event);
    }

    [Fact]
    public void After_rejection_a_revised_amount_can_be_resubmitted_without_retracting_first()
    {
        var claim = NewClaimWithPolicy();
        var submitter = Guid.NewGuid();
        var submission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 50_000m, null, submitter, DateTimeOffset.UtcNow);
        claim.RejectReserveTransaction(submission.History!, Guid.NewGuid(), "Needs more detail", DateTimeOffset.UtcNow);

        var resubmission = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, 8_000m, "Revised, smaller amount", submitter, DateTimeOffset.UtcNow);

        Assert.True(resubmission.IsAllowed);
        Assert.Equal(ReserveApprovalStatus.AutoApproved, resubmission.History!.ApprovalStatus);
        Assert.Equal(8_000m, resubmission.Component!.CurrentAmount);
        Assert.Contains(resubmission.Component.History, h => h.Id == submission.History!.Id && h.ApprovalStatus == ReserveApprovalStatus.Rejected);
    }

    /// <summary>Test helper: submits and immediately approves a transaction as a Manager, for
    /// tests that need an already-approved reserve position set up before the behaviour under
    /// test runs.</summary>
    private static void ApproveAsManager(Claim claim, ReserveComponentType componentType, decimal amount, bool overrideAggregateCap = false)
    {
        var submission = claim.SubmitReserveTransaction(componentType, amount, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var toApprove = submission.History!.ApprovalStatus == ReserveApprovalStatus.PendingApproval
            ? submission.History
            : null;
        if (toApprove is not null)
        {
            claim.ApproveReserveTransaction(toApprove, Guid.NewGuid(), UserRole.Manager, overrideAggregateCap, DateTimeOffset.UtcNow);
        }
    }
}
