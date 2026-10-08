using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Api.IntegrationTests.Fixtures;
using ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;
using ClaimsModule.Application.Claims.Queries.GetClaimById;
using ClaimsModule.Application.Claims.Queries.GetClaims;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Application.Policies.Queries.SearchPolicies;
using ClaimsModule.Application.Reference.Queries.GetCauseOfLossCodes;
using ClaimsModule.Application.Users.Queries.GetCurrentUser;
using Xunit;

namespace ClaimsModule.Api.IntegrationTests;

/// <summary>
/// Ticket 04's FNOL intake backend: create/list/view claims against the real SQL Server test
/// container (ADR-0001 repository+UoW path for creation, ADR-0002 direct-projection reads).
/// </summary>
public class ClaimsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> GetActiveCauseOfLossCodeIdAsync(string code = "COL-FIRE")
    {
        var response = await _client.GetAsync("/api/reference/cause-of-loss-codes");
        var codes = await response.Content.ReadFromJsonAsync<List<CauseOfLossCodeDto>>();
        return codes!.Single(c => c.Code == code).Id;
    }

    private async Task<Guid> GetPolicyIdAsync(string policyNumber)
    {
        var response = await _client.GetAsync($"/api/policies/search?q={policyNumber}");
        var policies = await response.Content.ReadFromJsonAsync<List<PolicySummaryDto>>();
        return policies!.Single(p => p.PolicyNumber == policyNumber).Id;
    }

    private static object ValidBody(
        Guid causeOfLossCodeId,
        Guid? policyId = null,
        DateTimeOffset? lossDate = null,
        object? parties = null,
        object? riskObjects = null,
        Guid? assignedHandlerUserId = null) => new
    {
        policyId,
        lossEvent = new
        {
            lossDate = lossDate ?? DateTimeOffset.UtcNow.AddDays(-3),
            lossDescription = "A delivery van collided with the loading dock, causing structural damage.",
            lossLocation = "123 Harbor Way, Portside",
            causeOfLossCodeId,
            estimatedLossAmount = 15000m,
            policeReportNumber = (string?)null
        },
        assignedHandlerUserId,
        parties = parties ?? new[]
        {
            new { role = "Claimant", partyType = "Person", firstName = "Jane", lastName = "Doe" }
        },
        riskObjects = riskObjects ?? new[]
        {
            new { assetType = "Vehicle", assetDescription = "2022 Ford Transit delivery van", isPrimary = true }
        }
    };

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/claims", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_a_valid_minimal_body_returns_201_with_a_generated_claim_number()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();

        var response = await _client.PostAsJsonAsync("/api/claims", ValidBody(causeId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateClaimResponseDto>();
        Assert.NotNull(result);
        Assert.Equal("Draft", result.Status);
        Assert.Matches(@"^CLM-\d{4}-\d{7}$", result.ClaimNumber);
        Assert.Equal(DateTime.UtcNow.Year.ToString(), result.ClaimNumber.Split('-')[1]);

        // No policy linked at all (BR-C-06) is a non-blocking Warning.
        Assert.Contains(result.Warnings, w => w.Contains("No policy linked"));

        Assert.EndsWith($"/api/claims/{result.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Create_generates_sequential_claim_numbers_with_no_gaps_for_the_same_org_and_year()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();

        var firstResponse = await _client.PostAsJsonAsync("/api/claims", ValidBody(causeId));
        var secondResponse = await _client.PostAsJsonAsync("/api/claims", ValidBody(causeId));

        var first = await firstResponse.Content.ReadFromJsonAsync<CreateClaimResponseDto>();
        var second = await secondResponse.Content.ReadFromJsonAsync<CreateClaimResponseDto>();

        var firstSequence = int.Parse(first!.ClaimNumber.Split('-')[2]);
        var secondSequence = int.Parse(second!.ClaimNumber.Split('-')[2]);

        Assert.Equal(firstSequence + 1, secondSequence);
    }

    [Fact]
    public async Task Create_under_concurrent_submissions_never_assigns_duplicate_claim_numbers()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();

        // Exercises ADR-0008's actual concurrency guarantee (atomic UPDATE ... OUTPUT INSERTED
        // under real parallel requests), not just two sequential calls.
        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => _client.PostAsJsonAsync("/api/claims", ValidBody(causeId))));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var claimNumbers = await Task.WhenAll(
            responses.Select(async r => (await r.Content.ReadFromJsonAsync<CreateClaimResponseDto>())!.ClaimNumber));

        Assert.Equal(claimNumbers.Length, claimNumbers.Distinct().Count());
    }

    [Fact]
    public async Task Create_with_a_future_loss_date_returns_400()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/claims", ValidBody(causeId, lossDate: DateTimeOffset.UtcNow.AddDays(5)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Loss date cannot be in the future.", body);
    }

    [Fact]
    public async Task Create_with_a_loss_description_under_20_characters_returns_400()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();

        var response = await _client.PostAsJsonAsync("/api/claims", new
        {
            policyId = (Guid?)null,
            lossEvent = new
            {
                lossDate = DateTimeOffset.UtcNow.AddDays(-1),
                lossDescription = "Too short.",
                lossLocation = (string?)null,
                causeOfLossCodeId = causeId,
                estimatedLossAmount = (decimal?)null,
                policeReportNumber = (string?)null
            },
            assignedHandlerUserId = (Guid?)null,
            parties = Array.Empty<object>(),
            riskObjects = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Loss description is required and must be at least 20 characters.", text);
    }

    [Fact]
    public async Task Create_with_an_inactive_or_unknown_cause_of_loss_code_returns_400()
    {
        await _client.LoginAsAsync("handler1");

        var response = await _client.PostAsJsonAsync("/api/claims", ValidBody(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cause of loss code is not recognised or is inactive.", text);
    }

    [Fact]
    public async Task Create_with_an_unknown_policy_id_returns_400()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();

        var response = await _client.PostAsJsonAsync("/api/claims", ValidBody(causeId, policyId: Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Linked policy was not found.", text);
    }

    [Fact]
    public async Task Create_with_a_loss_date_outside_the_linked_policys_effective_period_warns_but_still_creates()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();
        var expiredPolicyId = await GetPolicyIdAsync("POL-2023-000099"); // FRS's own expired example

        var response = await _client.PostAsJsonAsync(
            "/api/claims",
            ValidBody(causeId, policyId: expiredPolicyId, lossDate: new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateClaimResponseDto>();
        Assert.Contains(result!.Warnings, w => w.Contains("Loss date is outside the policy effective period."));
    }

    [Fact]
    public async Task Create_with_a_linked_active_policy_denormalises_policy_number_and_client_name()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();
        var policyId = await GetPolicyIdAsync("POL-2024-001001"); // Meridian Transport LLC, 2024-01-01..2026-12-31

        var createResponse = await _client.PostAsJsonAsync(
            "/api/claims",
            ValidBody(causeId, policyId: policyId, lossDate: new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateClaimResponseDto>();
        Assert.DoesNotContain(created!.Warnings, w => w.Contains("policy"));

        var detailResponse = await _client.GetAsync($"/api/claims/{created.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ClaimDetailDto>();

        Assert.Equal("POL-2024-001001", detail!.PolicyNumber);
        Assert.Equal("Meridian Transport LLC", detail.ClientName);
    }

    [Fact]
    public async Task Create_writes_a_CLAIM_CREATED_audit_entry_visible_via_the_audit_endpoint()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/claims", ValidBody(causeId));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateClaimResponseDto>();

        var auditResponse = await _client.GetAsync($"/api/claims/{created!.Id}/audit");

        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.NotNull(audit);
        Assert.Contains(audit.Items, a => a.EventType == "CLAIM_CREATED");
    }

    [Fact]
    public async Task Audit_log_for_an_unknown_claim_returns_404()
    {
        await _client.LoginAsAsync("handler1");

        var response = await _client.GetAsync($"/api/claims/{Guid.NewGuid()}/audit");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_for_an_unknown_claim_returns_404()
    {
        await _client.LoginAsAsync("handler1");

        var response = await _client.GetAsync($"/api/claims/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_without_a_token_returns_401()
    {
        var response = await _client.GetAsync($"/api/claims/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/claims");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_returns_full_detail_including_loss_event_parties_and_risk_objects()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/claims", ValidBody(
            causeId,
            parties: new[]
            {
                new { role = "Claimant", partyType = "Person", firstName = "Jane", lastName = "Doe" },
                new { role = "Witness", partyType = "Person", firstName = "Wendy", lastName = "Witness" }
            },
            riskObjects: new[]
            {
                new { assetType = "Vehicle", assetDescription = "2022 Ford Transit delivery van", isPrimary = true },
                new { assetType = "Property", assetDescription = "Loading dock structure", isPrimary = false }
            }));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateClaimResponseDto>();

        var detailResponse = await _client.GetAsync($"/api/claims/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<ClaimDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(created.ClaimNumber, detail.ClaimNumber);
        Assert.Equal("Draft", detail.Status);
        Assert.Equal("COL-FIRE", detail.LossEvent.CauseOfLossCode);
        Assert.Equal(2, detail.Parties.Count);
        Assert.Contains(detail.Parties, p => p.Role == "Claimant" && p.FirstName == "Jane");
        Assert.Contains(detail.Parties, p => p.Role == "Witness");
        Assert.Equal(2, detail.RiskObjects.Count);
        Assert.Contains(detail.RiskObjects, r => r.AssetType == "Vehicle" && r.IsPrimary);
        Assert.Empty(detail.Reserves);
        Assert.Empty(detail.Documents);
    }

    [Fact]
    public async Task List_supports_free_text_search_by_claim_number()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/claims", ValidBody(causeId));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateClaimResponseDto>();

        var response = await _client.GetAsync($"/api/claims?search={created!.ClaimNumber}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ClaimSummaryDto>>();
        Assert.NotNull(page);
        Assert.Contains(page.Items, c => c.Id == created.Id);
    }

    [Fact]
    public async Task List_supports_free_text_search_by_client_name()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();
        var policyId = await GetPolicyIdAsync("POL-2024-001002"); // Harborview Properties Inc

        var createResponse = await _client.PostAsJsonAsync(
            "/api/claims",
            ValidBody(causeId, policyId: policyId, lossDate: new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero)));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateClaimResponseDto>();

        var response = await _client.GetAsync("/api/claims?search=Harborview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ClaimSummaryDto>>();
        Assert.NotNull(page);
        Assert.Contains(page.Items, c => c.Id == created!.Id && c.ClientName == "Harborview Properties Inc");
    }

    [Fact]
    public async Task List_filters_by_status_date_range_and_cause_of_loss()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();
        var lossDate = new DateTimeOffset(2025, 9, 15, 0, 0, 0, TimeSpan.Zero);

        var createResponse = await _client.PostAsJsonAsync("/api/claims", ValidBody(causeId, lossDate: lossDate));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateClaimResponseDto>();

        var response = await _client.GetAsync(
            "/api/claims?status=Draft&dateFrom=2025-09-01&dateTo=2025-09-30&causeOfLossCode=COL-FIRE");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ClaimSummaryDto>>();
        Assert.NotNull(page);
        Assert.Contains(page.Items, c => c.Id == created!.Id);
        Assert.All(page.Items, c => Assert.Equal("Draft", c.Status));
    }

    [Fact]
    public async Task List_filters_by_assigned_handler()
    {
        await _client.LoginAsAsync("handler1");
        var meResponse = await _client.GetAsync("/api/users/me");
        var me = await meResponse.Content.ReadFromJsonAsync<CurrentUserDto>();
        var causeId = await GetActiveCauseOfLossCodeIdAsync();

        var createResponse = await _client.PostAsJsonAsync(
            "/api/claims", ValidBody(causeId, assignedHandlerUserId: me!.UserId));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateClaimResponseDto>();

        var response = await _client.GetAsync($"/api/claims?assignedHandlerUserId={me.UserId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ClaimSummaryDto>>();
        Assert.NotNull(page);
        Assert.Contains(page.Items, c => c.Id == created!.Id);
    }

    [Fact]
    public async Task List_paginates_results()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();

        for (var i = 0; i < 3; i++)
        {
            await _client.PostAsJsonAsync("/api/claims", ValidBody(causeId));
        }

        var response = await _client.GetAsync("/api/claims?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ClaimSummaryDto>>();
        Assert.NotNull(page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(1, page.Page);
        Assert.Equal(2, page.Items.Count);
        Assert.True(page.TotalCount >= 3);
    }

    /// <summary>Mirrors CreateClaimResult's shape without taking a dependency on the
    /// Application assembly's IHasValidationWarnings interface member for deserialisation.</summary>
    private record CreateClaimResponseDto(Guid Id, string ClaimNumber, string Status, List<string> Warnings);
}
