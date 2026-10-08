using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Api.IntegrationTests.Fixtures;
using ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Application.Reference.Queries.GetCauseOfLossCodes;
using Xunit;

namespace ClaimsModule.Api.IntegrationTests;

/// <summary>
/// Ticket 06's status transitions: HTTP-seam coverage for the endpoint/authorization/audit-log
/// wiring per spec.md's Testing Decisions — the pure transition-validation branching itself is
/// covered by ClaimsModule.Domain.UnitTests' narrow Claim-only seam instead.
/// </summary>
public class ClaimStatusTransitionTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> GetActiveCauseOfLossCodeIdAsync()
    {
        var response = await _client.GetAsync("/api/reference/cause-of-loss-codes");
        var codes = await response.Content.ReadFromJsonAsync<List<CauseOfLossCodeDto>>();
        return codes!.Single(c => c.Code == "COL-FIRE").Id;
    }

    private async Task<(Guid Id, string ClaimNumber)> CreateDraftClaimAsync(bool withClaimant = true)
    {
        var causeId = await GetActiveCauseOfLossCodeIdAsync();
        var response = await _client.PostAsJsonAsync("/api/claims", new
        {
            policyId = (Guid?)null,
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
            parties = withClaimant
                ? new[] { new { role = "Claimant", partyType = "Person", firstName = "Jane", lastName = "Doe" } }
                : Array.Empty<object>(),
            riskObjects = Array.Empty<object>()
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateClaimResponseDto>();
        return (created!.Id, created.ClaimNumber);
    }

    private Task<HttpResponseMessage> TransitionAsync(Guid claimId, string targetStatus, string? reason = null) =>
        _client.PutAsJsonAsync($"/api/claims/{claimId}/status", new { targetStatus, reason });

    [Fact]
    public async Task TransitionStatus_without_a_token_returns_401()
    {
        var response = await _client.PutAsJsonAsync($"/api/claims/{Guid.NewGuid()}/status", new { targetStatus = "Open" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TransitionStatus_for_an_unknown_claim_returns_404()
    {
        await _client.LoginAsAsync("handler1");

        var response = await TransitionAsync(Guid.NewGuid(), "Open");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TransitionStatus_to_a_status_not_listed_for_the_current_status_returns_422_with_valid_next_statuses()
    {
        await _client.LoginAsAsync("handler1");
        var (claimId, _) = await CreateDraftClaimAsync();

        var response = await TransitionAsync(claimId, "Closed");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Transition from Draft to Closed is not permitted.", body);
        Assert.Contains("validNextStatuses", body);
        Assert.Contains("Open", body);
    }

    [Fact]
    public async Task TransitionStatus_to_Open_without_a_Claimant_party_returns_422_with_the_unmet_condition()
    {
        await _client.LoginAsAsync("handler1");
        var (claimId, _) = await CreateDraftClaimAsync(withClaimant: false);

        var response = await TransitionAsync(claimId, "Open");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("At least one Claimant party is required to open a claim.", body);
        Assert.Contains("unmetConditions", body);
    }

    [Fact]
    public async Task TransitionStatus_to_Open_with_a_Claimant_party_succeeds_and_writes_an_audit_entry()
    {
        await _client.LoginAsAsync("handler1");
        var (claimId, _) = await CreateDraftClaimAsync();

        var response = await TransitionAsync(claimId, "Open");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TransitionClaimStatusResponseDto>();
        Assert.Equal("Open", result!.Status);

        var detail = await _client.GetAsync($"/api/claims/{claimId}");
        var detailBody = await detail.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"Open\"", detailBody);

        var auditResponse = await _client.GetAsync($"/api/claims/{claimId}/audit");
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.Contains(audit!.Items, a => a.EventType == "STATUS_CHANGED" && a.OldValue == "Draft" && a.NewValue == "Open");
    }

    [Fact]
    public async Task TransitionStatus_to_Closed_requires_at_least_Supervisor_role()
    {
        await _client.LoginAsAsync("handler1");
        var (claimId, _) = await CreateDraftClaimAsync();
        await TransitionAsync(claimId, "Open");

        var response = await TransitionAsync(claimId, "Closed", "All settled.");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Your role does not have authority to perform this transition.", body);
        Assert.Contains("requiredRole", body);
        Assert.Contains("Supervisor", body);
    }

    [Fact]
    public async Task TransitionStatus_to_Closed_as_Supervisor_succeeds_and_writes_a_CLAIM_CLOSED_audit_entry()
    {
        await _client.LoginAsAsync("handler1");
        var (claimId, _) = await CreateDraftClaimAsync();
        await TransitionAsync(claimId, "Open");

        await _client.LoginAsAsync("supervisor1");
        var response = await TransitionAsync(claimId, "Closed", "All settled.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TransitionClaimStatusResponseDto>();
        Assert.Equal("Closed", result!.Status);

        var auditResponse = await _client.GetAsync($"/api/claims/{claimId}/audit");
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.Contains(audit!.Items, a => a.EventType == "CLAIM_CLOSED" && a.NewValue == "All settled.");
    }

    [Fact]
    public async Task TransitionStatus_to_Reopened_requires_Supervisor_role_even_with_a_reason()
    {
        await _client.LoginAsAsync("handler1");
        var (claimId, _) = await CreateDraftClaimAsync();
        await TransitionAsync(claimId, "Open");
        await _client.LoginAsAsync("supervisor1");
        await TransitionAsync(claimId, "Closed", "All settled.");

        await _client.LoginAsAsync("handler1");
        var response = await TransitionAsync(claimId, "Reopened", "New damage found.");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Your role does not have authority to perform this transition.", body);
    }

    [Fact]
    public async Task TransitionStatus_to_Reopened_with_an_empty_reason_returns_422()
    {
        await _client.LoginAsAsync("handler1");
        var (claimId, _) = await CreateDraftClaimAsync();
        await TransitionAsync(claimId, "Open");
        await _client.LoginAsAsync("supervisor1");
        await TransitionAsync(claimId, "Closed", "All settled.");

        var response = await TransitionAsync(claimId, "Reopened", "   ");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("A reopen reason is required.", body);
    }

    [Fact]
    public async Task TransitionStatus_to_Reopened_as_Supervisor_with_a_reason_lands_the_claim_on_Open_and_writes_a_CLAIM_REOPENED_audit_entry()
    {
        await _client.LoginAsAsync("handler1");
        var (claimId, _) = await CreateDraftClaimAsync();
        await TransitionAsync(claimId, "Open");
        await _client.LoginAsAsync("supervisor1");
        await TransitionAsync(claimId, "Closed", "All settled.");

        var response = await TransitionAsync(claimId, "Reopened", "New damage found.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TransitionClaimStatusResponseDto>();
        Assert.Equal("Open", result!.Status);

        var auditResponse = await _client.GetAsync($"/api/claims/{claimId}/audit");
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.Contains(audit!.Items, a => a.EventType == "CLAIM_REOPENED" && a.NewValue == "New damage found.");
    }

    [Fact]
    public async Task TransitionStatus_to_Withdrawn_without_a_reason_returns_422()
    {
        await _client.LoginAsAsync("handler1");
        var (claimId, _) = await CreateDraftClaimAsync();
        await TransitionAsync(claimId, "Open");

        var response = await TransitionAsync(claimId, "Withdrawn");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("A withdrawal reason is required.", body);
    }

    [Fact]
    public async Task TransitionStatus_to_Withdrawn_with_a_reason_succeeds()
    {
        await _client.LoginAsAsync("handler1");
        var (claimId, _) = await CreateDraftClaimAsync();
        await TransitionAsync(claimId, "Open");

        var response = await TransitionAsync(claimId, "Withdrawn", "Claimant withdrew the claim.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TransitionClaimStatusResponseDto>();
        Assert.Equal("Withdrawn", result!.Status);
    }

    private record CreateClaimResponseDto(Guid Id, string ClaimNumber, string Status, List<string> Warnings);

    private record TransitionClaimStatusResponseDto(Guid Id, string Status);
}
