using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Events;

namespace ClaimsModule.Domain.UnitTests.Entities;

/// <summary>
/// Ticket 06 / spec.md's narrow "Claim only" unit-test seam: the status transition state
/// machine's combinatorial branching (valid/invalid pairs, role authority, Open/Closed/Reopened/
/// Withdrawn's extra conditions) is exercised here purely in-memory, with no HTTP or database —
/// exactly the seam spec.md's Testing Decisions justify for this branching.
/// </summary>
public class ClaimStatusTransitionTests
{
    private static Claim NewDraftClaim(params ClaimParty[] parties)
    {
        var claim = Claim.Create(
            organizationId: Guid.NewGuid(),
            claimNumber: "CLM-2026-0000001",
            policyId: null,
            policyNumber: null,
            clientName: "Jane Doe",
            assignedHandlerUserId: null,
            lossEvent: new LossEvent
            {
                LossDate = DateTimeOffset.UtcNow.AddDays(-1),
                LossDescription = "A sufficiently long loss description for the aggregate.",
                CauseOfLossCodeId = Guid.NewGuid()
            },
            parties: parties,
            riskObjects: []);

        claim.ClearDomainEvents();
        return claim;
    }

    private static ClaimParty Claimant(bool isActive = true) =>
        new() { Role = ClaimPartyRole.Claimant, PartyType = ClaimPartyType.Person, FirstName = "Jane", LastName = "Doe", IsActive = isActive };

    private static ClaimStatusTransition Rule(ClaimStatus from, ClaimStatus to, UserRole requiredRole) =>
        new() { FromStatus = from, ToStatus = to, RequiredPermission = requiredRole };

    [Fact]
    public void ValidateTransition_rejects_a_pair_not_present_in_the_allowed_transitions_and_lists_valid_next_statuses()
    {
        var claim = NewDraftClaim(Claimant());
        var allowed = new[] { Rule(ClaimStatus.Draft, ClaimStatus.Open, UserRole.Handler) };

        var result = claim.ValidateTransition(ClaimStatus.Closed, allowed, UserRole.Manager, null);

        Assert.False(result.IsAllowed);
        Assert.Equal("Transition from Draft to Closed is not permitted.", result.Message);
        Assert.Equal([ClaimStatus.Open], result.ValidNextStatuses);
    }

    [Fact]
    public void ValidateTransition_rejects_when_the_actors_role_is_below_the_rules_required_permission()
    {
        var claim = NewDraftClaim(Claimant());
        var allowed = new[] { Rule(ClaimStatus.Draft, ClaimStatus.Open, UserRole.Supervisor) };

        var result = claim.ValidateTransition(ClaimStatus.Open, allowed, UserRole.Handler, null);

        Assert.False(result.IsAllowed);
        Assert.Equal("Your role does not have authority to perform this transition.", result.Message);
        Assert.Equal(UserRole.Supervisor, result.RequiredRole);
    }

    [Theory]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Manager)]
    public void ValidateTransition_allows_a_role_above_the_required_minimum(UserRole actorRole)
    {
        var claim = NewDraftClaim(Claimant());
        var allowed = new[] { Rule(ClaimStatus.Draft, ClaimStatus.Open, UserRole.Handler) };

        var result = claim.ValidateTransition(ClaimStatus.Open, allowed, actorRole, null);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void ValidateTransition_to_Open_is_blocked_when_the_claim_has_no_active_Claimant_party()
    {
        var claim = NewDraftClaim();
        var allowed = new[] { Rule(ClaimStatus.Draft, ClaimStatus.Open, UserRole.Handler) };

        var result = claim.ValidateTransition(ClaimStatus.Open, allowed, UserRole.Handler, null);

        Assert.False(result.IsAllowed);
        Assert.Contains("At least one Claimant party is required to open a claim.", result.UnmetConditions!);
    }

    [Fact]
    public void ValidateTransition_to_Open_is_blocked_when_the_only_Claimant_party_is_inactive()
    {
        var claim = NewDraftClaim(Claimant(isActive: false));
        var allowed = new[] { Rule(ClaimStatus.Draft, ClaimStatus.Open, UserRole.Handler) };

        var result = claim.ValidateTransition(ClaimStatus.Open, allowed, UserRole.Handler, null);

        Assert.False(result.IsAllowed);
        Assert.Contains("At least one Claimant party is required to open a claim.", result.UnmetConditions!);
    }

    [Fact]
    public void ValidateTransition_to_Open_succeeds_with_an_active_Claimant_party()
    {
        var claim = NewDraftClaim(Claimant());
        var allowed = new[] { Rule(ClaimStatus.Draft, ClaimStatus.Open, UserRole.Handler) };

        var result = claim.ValidateTransition(ClaimStatus.Open, allowed, UserRole.Handler, null);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void ValidateTransition_to_Closed_is_blocked_when_the_claim_has_no_active_Claimant_party()
    {
        var claim = NewDraftClaim();
        claim.Status = ClaimStatus.Open;
        var allowed = new[] { Rule(ClaimStatus.Open, ClaimStatus.Closed, UserRole.Supervisor) };

        var result = claim.ValidateTransition(ClaimStatus.Closed, allowed, UserRole.Supervisor, "All settled.");

        Assert.False(result.IsAllowed);
        Assert.Contains("At least one Claimant party is required to close a claim.", result.UnmetConditions!);
    }

    [Fact]
    public void ValidateTransition_to_Closed_succeeds_when_all_closure_conditions_are_met()
    {
        var claim = NewDraftClaim(Claimant());
        claim.Status = ClaimStatus.Open;
        var allowed = new[] { Rule(ClaimStatus.Open, ClaimStatus.Closed, UserRole.Supervisor) };

        var result = claim.ValidateTransition(ClaimStatus.Closed, allowed, UserRole.Supervisor, "All settled.");

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void ValidateTransition_to_Reopened_is_blocked_when_the_reason_is_empty()
    {
        var claim = NewDraftClaim(Claimant());
        claim.Status = ClaimStatus.Closed;
        var allowed = new[] { Rule(ClaimStatus.Closed, ClaimStatus.Reopened, UserRole.Supervisor) };

        var result = claim.ValidateTransition(ClaimStatus.Reopened, allowed, UserRole.Supervisor, "   ");

        Assert.False(result.IsAllowed);
        Assert.Contains("A reopen reason is required.", result.UnmetConditions!);
    }

    [Fact]
    public void ValidateTransition_to_Reopened_is_blocked_for_a_Handler_even_with_a_reason()
    {
        var claim = NewDraftClaim(Claimant());
        claim.Status = ClaimStatus.Closed;
        var allowed = new[] { Rule(ClaimStatus.Closed, ClaimStatus.Reopened, UserRole.Supervisor) };

        var result = claim.ValidateTransition(ClaimStatus.Reopened, allowed, UserRole.Handler, "New damage found.");

        Assert.False(result.IsAllowed);
        Assert.Equal(UserRole.Supervisor, result.RequiredRole);
    }

    [Fact]
    public void ValidateTransition_to_Reopened_succeeds_for_a_Supervisor_with_a_non_empty_reason()
    {
        var claim = NewDraftClaim(Claimant());
        claim.Status = ClaimStatus.Closed;
        var allowed = new[] { Rule(ClaimStatus.Closed, ClaimStatus.Reopened, UserRole.Supervisor) };

        var result = claim.ValidateTransition(ClaimStatus.Reopened, allowed, UserRole.Supervisor, "New damage found.");

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void ValidateTransition_to_Withdrawn_is_blocked_when_the_reason_is_empty()
    {
        var claim = NewDraftClaim(Claimant());
        claim.Status = ClaimStatus.Open;
        var allowed = new[] { Rule(ClaimStatus.Open, ClaimStatus.Withdrawn, UserRole.Handler) };

        var result = claim.ValidateTransition(ClaimStatus.Withdrawn, allowed, UserRole.Handler, null);

        Assert.False(result.IsAllowed);
        Assert.Contains("A withdrawal reason is required.", result.UnmetConditions!);
    }

    [Fact]
    public void ApplyTransition_to_Reopened_lands_the_claim_on_Open_and_raises_ClaimReopenedEvent()
    {
        var claim = NewDraftClaim(Claimant());
        claim.Status = ClaimStatus.Closed;

        claim.ApplyTransition(ClaimStatus.Reopened, "New damage found.", DateTimeOffset.UtcNow);

        Assert.Equal(ClaimStatus.Open, claim.Status);
        var @event = Assert.Single(claim.DomainEvents);
        Assert.IsType<ClaimReopenedEvent>(@event);
    }

    [Fact]
    public void ApplyTransition_to_Closed_sets_ClosedAt_and_ClosureReason_and_raises_ClaimClosedEvent()
    {
        var claim = NewDraftClaim(Claimant());
        claim.Status = ClaimStatus.Open;
        var now = DateTimeOffset.UtcNow;

        claim.ApplyTransition(ClaimStatus.Closed, "All settled.", now);

        Assert.Equal(ClaimStatus.Closed, claim.Status);
        Assert.Equal(now, claim.ClosedAt);
        Assert.Equal("All settled.", claim.ClosureReason);
        var @event = Assert.Single(claim.DomainEvents);
        Assert.IsType<ClaimClosedEvent>(@event);
    }

    [Fact]
    public void ApplyTransition_to_a_routine_status_sets_Status_and_raises_ClaimStatusChangedEvent()
    {
        var claim = NewDraftClaim(Claimant());

        claim.ApplyTransition(ClaimStatus.Open, null, DateTimeOffset.UtcNow);

        Assert.Equal(ClaimStatus.Open, claim.Status);
        var @event = Assert.Single(claim.DomainEvents);
        Assert.IsType<ClaimStatusChangedEvent>(@event);
    }
}
