using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Api.IntegrationTests.Fixtures;
using ClaimsModule.Application.Policies.Queries.GetPolicyCoverage;
using ClaimsModule.Application.Policies.Queries.SearchPolicies;
using Xunit;

namespace ClaimsModule.Api.IntegrationTests;

/// <summary>
/// Ticket 03's simulated-policy endpoints: search and coverage lookup, seeded via the
/// independently re-runnable SQL script (ADR-0002: direct EF projection, HTTP seam).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class PoliciesTests(ApiWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Search_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/policies/search");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_by_partial_policy_number_finds_the_seeded_policy()
    {
        await _client.LoginAsAsync("handler1");

        var response = await _client.GetAsync("/api/policies/search?q=001001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var policies = await response.Content.ReadFromJsonAsync<List<PolicySummaryDto>>();
        Assert.NotNull(policies);
        Assert.Contains(policies, p => p.PolicyNumber == "POL-2024-001001" && p.ClientName == "Meridian Transport LLC");
    }

    [Fact]
    public async Task Search_by_partial_client_name_finds_the_seeded_policy()
    {
        await _client.LoginAsAsync("handler1");

        var response = await _client.GetAsync("/api/policies/search?q=Harborview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var policies = await response.Content.ReadFromJsonAsync<List<PolicySummaryDto>>();
        Assert.NotNull(policies);
        Assert.Contains(policies, p => p.PolicyNumber == "POL-2024-001002");
    }

    [Fact]
    public async Task Search_includes_the_expired_policy_example_from_the_FRS()
    {
        await _client.LoginAsAsync("handler1");

        var response = await _client.GetAsync("/api/policies/search?q=Archived");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var policies = await response.Content.ReadFromJsonAsync<List<PolicySummaryDto>>();
        Assert.NotNull(policies);
        var expired = Assert.Single(policies, p => p.PolicyNumber == "POL-2023-000099");
        Assert.Equal("Expired", expired.Status);
        Assert.Equal(new DateOnly(2020, 1, 1), expired.EffectiveDate);
        Assert.Equal(new DateOnly(2021, 12, 31), expired.ExpirationDate);
    }

    [Fact]
    public async Task Get_coverage_without_a_token_returns_401()
    {
        var response = await _client.GetAsync($"/api/policies/{Guid.NewGuid()}/coverage");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_coverage_for_an_unknown_policy_returns_404()
    {
        await _client.LoginAsAsync("handler1");

        var response = await _client.GetAsync($"/api/policies/{Guid.NewGuid()}/coverage");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_coverage_returns_the_seeded_coverages_for_the_policy()
    {
        await _client.LoginAsAsync("handler1");

        var searchResponse = await _client.GetAsync("/api/policies/search?q=001001");
        var policies = await searchResponse.Content.ReadFromJsonAsync<List<PolicySummaryDto>>();
        var policyId = policies!.Single(p => p.PolicyNumber == "POL-2024-001001").Id;

        var response = await _client.GetAsync($"/api/policies/{policyId}/coverage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var coverages = await response.Content.ReadFromJsonAsync<List<PolicyCoverageDto>>();
        Assert.NotNull(coverages);
        var types = coverages.Select(c => c.CoverageType).OrderBy(t => t).ToList();
        Assert.Equal(new[] { "Cargo", "Vehicle" }, types);
    }
}
