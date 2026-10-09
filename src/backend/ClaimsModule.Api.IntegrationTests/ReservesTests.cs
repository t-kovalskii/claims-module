using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Api.IntegrationTests.Fixtures;
using ClaimsModule.Application.Claims.Commands.ApproveReserveTransaction;
using ClaimsModule.Application.Claims.Commands.RejectReserveTransaction;
using ClaimsModule.Application.Claims.Commands.RetractReserveTransaction;
using ClaimsModule.Application.Claims.Commands.SubmitReserveTransaction;
using ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;
using ClaimsModule.Application.Claims.Queries.GetClaimById;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Application.Policies.Queries.SearchPolicies;
using ClaimsModule.Application.Reference.Queries.GetCauseOfLossCodes;
using Xunit;

namespace ClaimsModule.Api.IntegrationTests;

/// <summary>
/// Ticket 07's reserve ledger: HTTP-seam coverage for the endpoint/authorization/persistence/
/// audit wiring per spec.md's Testing Decisions — the pure tier-routing/retraction branching
/// itself is covered by ClaimsModule.Domain.UnitTests' narrow Claim-only seam instead.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class ReservesTests(ApiWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> GetActiveCauseOfLossCodeIdAsync() =>
        (await (await _client.GetAsync("/api/reference/cause-of-loss-codes"))
            .Content.ReadFromJsonAsync<List<CauseOfLossCodeDto>>())!
        .Single(c => c.Code == "COL-FIRE").Id;

    private async Task<Guid> GetActivePolicyIdAsync()
    {
        var response = await _client.GetAsync("/api/policies/search?q=POL-2024-001001");
        var policies = await response.Content.ReadFromJsonAsync<List<PolicySummaryDto>>();
        return policies!.Single(p => p.PolicyNumber == "POL-2024-001001").Id;
    }

    private async Task<Guid> CreateClaimAsync(bool withPolicy = true)
    {
        var causeId = await GetActiveCauseOfLossCodeIdAsync();
        Guid? policyId = withPolicy ? await GetActivePolicyIdAsync() : null;

        var response = await _client.PostAsJsonAsync("/api/claims", new
        {
            policyId,
            lossEvent = new
            {
                lossDate = DateTimeOffset.UtcNow.AddDays(-3),
                lossDescription = "A delivery van collided with the loading dock, causing structural damage.",
                lossLocation = "123 Harbor Way, Portside",
                causeOfLossCodeId = causeId,
                estimatedLossAmount = 15000m,
                policeReportNumber = (string?)null
            },
            assignedHandlerUserId = (Guid?)null,
            parties = new[] { new { role = "Claimant", partyType = "Person", firstName = "Jane", lastName = "Doe" } },
            riskObjects = Array.Empty<object>()
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateClaimResponseDto>();
        return created!.Id;
    }

    private Task<HttpResponseMessage> SubmitReserveAsync(Guid claimId, string componentType, decimal amount, string? changeReason = "Initial reserve") =>
        _client.PostAsJsonAsync($"/api/claims/{claimId}/reserves", new { componentType, amount, changeReason });

    private Task<HttpResponseMessage> RetractAsync(Guid claimId, Guid reserveHistoryId) =>
        _client.PostAsync($"/api/claims/{claimId}/reserves/{reserveHistoryId}/retract", content: null);

    private Task<HttpResponseMessage> ApproveAsync(Guid claimId, Guid reserveHistoryId, bool overrideAggregateCap = false) =>
        _client.PostAsJsonAsync($"/api/claims/{claimId}/reserves/{reserveHistoryId}/approve", new { overrideAggregateCap });

    private Task<HttpResponseMessage> RejectAsync(Guid claimId, Guid reserveHistoryId, string rejectionReason = "Not substantiated") =>
        _client.PostAsJsonAsync($"/api/claims/{claimId}/reserves/{reserveHistoryId}/reject", new { rejectionReason });

    private async Task<ClaimDetailDto> GetClaimDetailAsync(Guid claimId)
    {
        var response = await _client.GetAsync($"/api/claims/{claimId}");
        return (await response.Content.ReadFromJsonAsync<ClaimDetailDto>())!;
    }

    /// <summary>GL posting (Ticket 08) runs as a real, fire-and-forget Hangfire job against the
    /// same test SQL Server container — polls the claim's audit log for a bounded time rather
    /// than asserting on it immediately after the triggering HTTP call returns.</summary>
    private async Task<PagedResult<ClaimAuditLogDto>> PollAuditLogUntilAsync(Guid claimId, string eventType, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        PagedResult<ClaimAuditLogDto> audit;
        do
        {
            var response = await _client.GetAsync($"/api/claims/{claimId}/audit");
            audit = (await response.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>())!;
            if (audit.Items.Any(a => a.EventType == eventType))
            {
                return audit;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        } while (DateTime.UtcNow < deadline);

        return audit;
    }

    [Fact]
    public async Task SubmitReserve_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/claims/{Guid.NewGuid()}/reserves", new { componentType = "Indemnity", amount = 5000m, changeReason = "x" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubmitReserve_for_an_unknown_claim_returns_404()
    {
        await _client.LoginAsAsync("handler1");

        var response = await SubmitReserveAsync(Guid.NewGuid(), "Indemnity", 5000m);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SubmitReserve_without_a_linked_policy_returns_422()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync(withPolicy: false);

        var response = await SubmitReserveAsync(claimId, "Indemnity", 5000m);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("A policy must be linked to the claim before reserves can be opened.", body);
    }

    [Theory]
    [InlineData("Indemnity", 0)]
    [InlineData("Indemnity", -500)]
    [InlineData("Expense", 0)]
    [InlineData("ALAE", -1)]
    public async Task SubmitReserve_with_a_non_positive_amount_for_a_non_subrogation_component_returns_400(string componentType, decimal amount)
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();

        var response = await SubmitReserveAsync(claimId, componentType, amount);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Reserve amount must be greater than zero.", body);
    }

    [Fact]
    public async Task SubmitReserve_with_a_zero_amount_for_SubrogationRecoverable_returns_400()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();

        var response = await SubmitReserveAsync(claimId, "SubrogationRecoverable", 0m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("SubrogationRecoverable amount must not be zero.", body);
    }

    [Fact]
    public async Task SubmitReserve_allows_a_negative_amount_for_SubrogationRecoverable()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();

        var response = await SubmitReserveAsync(claimId, "SubrogationRecoverable", -8000m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();
        Assert.Equal(-8000m, result!.CurrentAmount);
        Assert.Equal("AutoApproved", result.ApprovalStatus);
    }

    [Fact]
    public async Task SubmitReserve_at_10000_is_AutoApproved_updates_CurrentAmount_and_writes_a_RESERVE_AUTO_APPROVED_audit_entry()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();

        var response = await SubmitReserveAsync(claimId, "Indemnity", 10_000m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();
        Assert.Equal("AutoApproved", result!.ApprovalStatus);
        Assert.Equal(10_000m, result.CurrentAmount);
        Assert.Equal("Add", result.TransactionType);

        var detail = await GetClaimDetailAsync(claimId);
        var component = Assert.Single(detail.Reserves);
        Assert.Equal("Indemnity", component.ComponentType);
        Assert.Equal(10_000m, component.CurrentAmount);
        Assert.Equal("AutoApproved", component.ApprovalStatus);
        Assert.Null(component.PendingAmount);

        var auditResponse = await _client.GetAsync($"/api/claims/{claimId}/audit");
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.Contains(audit!.Items, a => a.EventType == "RESERVE_AUTO_APPROVED");
    }

    [Fact]
    public async Task SubmitReserve_above_10000_is_PendingApproval_leaves_CurrentAmount_at_zero_and_writes_a_RESERVE_CREATED_audit_entry()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();

        var response = await SubmitReserveAsync(claimId, "Indemnity", 50_000m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();
        Assert.Equal("PendingApproval", result!.ApprovalStatus);
        Assert.Equal(0m, result.CurrentAmount);

        var detail = await GetClaimDetailAsync(claimId);
        var component = Assert.Single(detail.Reserves);
        Assert.Equal(0m, component.CurrentAmount);
        Assert.Equal("PendingApproval", component.ApprovalStatus);
        Assert.Equal(50_000m, component.PendingAmount);

        var auditResponse = await _client.GetAsync($"/api/claims/{claimId}/audit");
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.Contains(audit!.Items, a => a.EventType == "RESERVE_CREATED");
    }

    [Fact]
    public async Task SubmitReserve_a_second_time_while_the_first_is_PendingApproval_returns_422()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        await SubmitReserveAsync(claimId, "Indemnity", 50_000m);

        var response = await SubmitReserveAsync(claimId, "Indemnity", 20_000m, "Revised amount");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("retract it before submitting a revised amount", body);
    }

    [Fact]
    public async Task SubmitReserve_a_second_time_against_an_AutoApproved_component_is_an_Adjust_transaction_and_accumulates()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        await SubmitReserveAsync(claimId, "Expense", 1000m);

        var response = await SubmitReserveAsync(claimId, "Expense", 500m, "Top-up");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();
        Assert.Equal("Adjust", result!.TransactionType);
        Assert.Equal(1500m, result.CurrentAmount);

        var detail = await GetClaimDetailAsync(claimId);
        var component = Assert.Single(detail.Reserves);
        Assert.Equal(2, component.History.Count);
    }

    [Fact]
    public async Task Retract_for_an_unknown_reserve_transaction_returns_404()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();

        var response = await RetractAsync(claimId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Retract_an_AutoApproved_transaction_returns_422()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        var submitResponse = await SubmitReserveAsync(claimId, "Indemnity", 5000m);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();

        var response = await RetractAsync(claimId, submitted!.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Only a reserve transaction pending approval can be retracted.", body);
    }

    [Fact]
    public async Task Retract_by_a_different_user_than_the_submitter_returns_422()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        var submitResponse = await SubmitReserveAsync(claimId, "Indemnity", 50_000m);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();

        await _client.LoginAsAsync("supervisor1");
        var response = await RetractAsync(claimId, submitted!.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Only the original submitter may retract this reserve transaction.", body);
    }

    [Fact]
    public async Task Retract_by_the_original_submitter_succeeds_and_writes_a_RESERVE_RETRACTED_audit_entry_then_allows_resubmission()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        var submitResponse = await SubmitReserveAsync(claimId, "Indemnity", 50_000m);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();

        var retractResponse = await RetractAsync(claimId, submitted!.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.OK, retractResponse.StatusCode);
        var retracted = await retractResponse.Content.ReadFromJsonAsync<RetractReserveTransactionResult>();
        Assert.Equal(0m, retracted!.CurrentAmount);

        var auditResponse = await _client.GetAsync($"/api/claims/{claimId}/audit");
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.Contains(audit!.Items, a => a.EventType == "RESERVE_RETRACTED");

        // A revised, smaller amount can now be submitted; the retracted record stays in history.
        var resubmitResponse = await SubmitReserveAsync(claimId, "Indemnity", 8000m, "Revised, smaller amount");
        Assert.Equal(HttpStatusCode.Created, resubmitResponse.StatusCode);

        var detail = await GetClaimDetailAsync(claimId);
        var component = Assert.Single(detail.Reserves);
        Assert.Equal(8000m, component.CurrentAmount);
        Assert.Equal(2, component.History.Count);
        Assert.Contains(component.History, h => h.Id == submitted.ReserveHistoryId && h.ApprovalStatus == "Cancelled");
    }

    [Fact]
    public async Task GetClaimById_reserve_history_records_previous_and_new_balance_reason_submitter_and_timestamp()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        await SubmitReserveAsync(claimId, "Indemnity", 5000m, "Initial loss estimate");

        var detail = await GetClaimDetailAsync(claimId);

        var component = Assert.Single(detail.Reserves);
        var entry = Assert.Single(component.History);
        Assert.Equal(0m, entry.PreviousBalance);
        Assert.Equal(5000m, entry.NewBalance);
        Assert.Equal("Initial loss estimate", entry.ChangeReason);
        Assert.NotNull(entry.SubmittedByUserId);
        Assert.True(entry.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    // --- Ticket 08: approval workflow HTTP-seam coverage (endpoint/role/audit/GL-posting wiring —
    // the authority-tier/self-approval/aggregate-cap *branching* itself is covered by
    // ClaimsModule.Domain.UnitTests' narrow Claim-only seam per spec.md's Testing Decisions) ---

    [Fact]
    public async Task ApproveReserve_without_a_token_returns_401()
    {
        var response = await ApproveAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApproveReserve_as_a_Handler_returns_403()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        var submitted = await (await SubmitReserveAsync(claimId, "Indemnity", 50_000m)).Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();

        var response = await ApproveAsync(claimId, submitted!.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApproveReserve_for_an_unknown_reserve_returns_404()
    {
        await _client.LoginAsAsync("supervisor1");
        var claimId = await CreateClaimAsync();

        var response = await ApproveAsync(claimId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApproveReserve_by_the_original_submitter_returns_422()
    {
        await _client.LoginAsAsync("supervisor1");
        var claimId = await CreateClaimAsync();
        var submitted = await (await SubmitReserveAsync(claimId, "Indemnity", 50_000m)).Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();

        var response = await ApproveAsync(claimId, submitted!.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Self-approval is not permitted.", body);
    }

    [Fact]
    public async Task ApproveReserve_above_100000_by_a_Supervisor_returns_422()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        var submitted = await (await SubmitReserveAsync(claimId, "Indemnity", 150_000m)).Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();

        await _client.LoginAsAsync("supervisor1");
        var response = await ApproveAsync(claimId, submitted!.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Manager approval is required", body);
    }

    [Fact]
    public async Task ApproveReserve_by_a_Supervisor_succeeds_updates_CurrentAmount_and_writes_a_RESERVE_APPROVED_audit_entry()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        var submitted = await (await SubmitReserveAsync(claimId, "Indemnity", 50_000m)).Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();

        await _client.LoginAsAsync("supervisor1");
        var response = await ApproveAsync(claimId, submitted!.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var approved = await response.Content.ReadFromJsonAsync<ApproveReserveTransactionResult>();
        Assert.Equal("Approved", approved!.ApprovalStatus);
        Assert.Equal(50_000m, approved.CurrentAmount);

        var detail = await GetClaimDetailAsync(claimId);
        var component = Assert.Single(detail.Reserves);
        Assert.Equal(50_000m, component.CurrentAmount);
        Assert.Equal("Approved", component.ApprovalStatus);

        var audit = await PollAuditLogUntilAsync(claimId, "RESERVE_APPROVED", TimeSpan.FromSeconds(5));
        Assert.Contains(audit.Items, a => a.EventType == "RESERVE_APPROVED");
    }

    [Fact]
    public async Task ApproveReserve_enqueues_GL_posting_which_eventually_marks_the_transaction_Posted()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        var submitted = await (await SubmitReserveAsync(claimId, "Indemnity", 50_000m)).Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();

        await _client.LoginAsAsync("supervisor1");
        await ApproveAsync(claimId, submitted!.ReserveHistoryId);

        var audit = await PollAuditLogUntilAsync(claimId, "GL_POSTING_SIMULATED", TimeSpan.FromSeconds(10));
        Assert.Contains(audit.Items, a => a.EventType == "GL_POSTING_SIMULATED");

        var detail = await GetClaimDetailAsync(claimId);
        var entry = Assert.Single(Assert.Single(detail.Reserves).History);
        Assert.Equal("Posted", entry.PostingStatus);
    }

    [Fact]
    public async Task SubmitReserve_AutoApproved_also_enqueues_GL_posting_which_eventually_marks_the_transaction_Posted()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        await SubmitReserveAsync(claimId, "Indemnity", 5000m);

        var audit = await PollAuditLogUntilAsync(claimId, "GL_POSTING_SIMULATED", TimeSpan.FromSeconds(10));
        Assert.Contains(audit.Items, a => a.EventType == "GL_POSTING_SIMULATED");

        var detail = await GetClaimDetailAsync(claimId);
        var entry = Assert.Single(Assert.Single(detail.Reserves).History);
        Assert.Equal("Posted", entry.PostingStatus);
    }

    [Fact]
    public async Task RejectReserve_without_a_reason_returns_400()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        var submitted = await (await SubmitReserveAsync(claimId, "Indemnity", 50_000m)).Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();

        await _client.LoginAsAsync("supervisor1");
        var response = await RejectAsync(claimId, submitted!.ReserveHistoryId, rejectionReason: "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RejectReserve_as_a_Handler_returns_403()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        var submitted = await (await SubmitReserveAsync(claimId, "Indemnity", 50_000m)).Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();

        var response = await RejectAsync(claimId, submitted!.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RejectReserve_succeeds_writes_a_RESERVE_REJECTED_audit_entry_then_allows_resubmission_without_retracting()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        var submitted = await (await SubmitReserveAsync(claimId, "Indemnity", 50_000m)).Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();

        await _client.LoginAsAsync("supervisor1");
        var response = await RejectAsync(claimId, submitted!.ReserveHistoryId, "Amount not substantiated");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rejected = await response.Content.ReadFromJsonAsync<RejectReserveTransactionResult>();
        Assert.Equal("Rejected", rejected!.ApprovalStatus);

        var audit = await _client.GetAsync($"/api/claims/{claimId}/audit");
        var auditPage = await audit.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.Contains(auditPage!.Items, a => a.EventType == "RESERVE_REJECTED");

        // No retract needed first — a Rejected row isn't PendingApproval, so a fresh submission
        // against the same component is immediately allowed (BR-R-04).
        await _client.LoginAsAsync("handler1");
        var resubmitResponse = await SubmitReserveAsync(claimId, "Indemnity", 8000m, "Revised, smaller amount");
        Assert.Equal(HttpStatusCode.Created, resubmitResponse.StatusCode);

        var detail = await GetClaimDetailAsync(claimId);
        var component = Assert.Single(detail.Reserves);
        Assert.Equal(8000m, component.CurrentAmount);
        Assert.Contains(component.History, h => h.Id == submitted.ReserveHistoryId && h.ApprovalStatus == "Rejected");
    }

    private record CreateClaimResponseDto(Guid Id, string ClaimNumber, string Status, List<string> Warnings);
}
