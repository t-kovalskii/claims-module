using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Api.IntegrationTests.Fixtures;
using ClaimsModule.Application.Reference.Queries.GetCauseOfLossCodes;
using ClaimsModule.Application.Reference.Queries.GetClaimStatuses;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClaimsModule.Api.IntegrationTests;

/// <summary>
/// Ticket 03's reference-data endpoints, exercised against the real SQL script seeded at app
/// start (ADR-0002: direct EF projection, so these are HTTP-seam tests, not Claim unit tests).
/// </summary>
public class ReferenceDataTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Cause_of_loss_codes_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/reference/cause-of-loss-codes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cause_of_loss_codes_returns_the_seeded_active_codes()
    {
        await _client.LoginAsAsync("handler1");

        var response = await _client.GetAsync("/api/reference/cause-of-loss-codes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var codes = await response.Content.ReadFromJsonAsync<List<CauseOfLossCodeDto>>();
        Assert.NotNull(codes);
        Assert.Contains(codes, c => c.Code == "COL-FIRE" && c.PerilCategory == "Property");
        Assert.Contains(codes, c => c.Code == "COL-OTHER" && c.PerilCategory == "General");
        Assert.True(codes.Count >= 10);
    }

    [Fact]
    public async Task Cause_of_loss_codes_excludes_inactive_codes()
    {
        await _client.LoginAsAsync("handler1");

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.CauseOfLossCodes.Add(new CauseOfLossCode
            {
                Code = $"COL-RETIRED-{Guid.NewGuid():N}",
                Name = "Retired Code",
                PerilCategory = PerilCategory.General,
                IsActive = false,
                SortOrder = 999
            });
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await _client.GetAsync("/api/reference/cause-of-loss-codes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var codes = await response.Content.ReadFromJsonAsync<List<CauseOfLossCodeDto>>();
        Assert.DoesNotContain(codes!, c => c.Name == "Retired Code");
    }

    [Fact]
    public async Task Cause_of_loss_codes_supports_filtering_by_peril_category()
    {
        await _client.LoginAsAsync("handler1");

        var response = await _client.GetAsync("/api/reference/cause-of-loss-codes?perilCategory=Weather");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var codes = await response.Content.ReadFromJsonAsync<List<CauseOfLossCodeDto>>();
        Assert.NotNull(codes);
        Assert.NotEmpty(codes);
        Assert.All(codes, c => Assert.Equal("Weather", c.PerilCategory));
        Assert.Contains(codes, c => c.Code == "COL-FLOOD");
        Assert.Contains(codes, c => c.Code == "COL-WIND");
    }

    [Fact]
    public async Task Claim_statuses_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/reference/claim-statuses");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Claim_statuses_returns_every_status_with_its_transitions_and_required_permission()
    {
        await _client.LoginAsAsync("handler1");

        var response = await _client.GetAsync("/api/reference/claim-statuses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var statuses = await response.Content.ReadFromJsonAsync<List<ClaimStatusDto>>();
        Assert.NotNull(statuses);

        // Every ClaimStatus enum value is represented, including terminal ones with no outgoing transitions.
        Assert.Equal(7, statuses.Count);
        Assert.Contains(statuses, s => s.Status == "Withdrawn" && s.ValidNextTransitions.Count == 0);

        var draft = statuses.Single(s => s.Status == "Draft");
        Assert.Contains(draft.ValidNextTransitions, t => t.ToStatus == "Open" && t.RequiredPermission == "Handler");

        var closed = statuses.Single(s => s.Status == "Closed");
        Assert.Contains(closed.ValidNextTransitions, t => t.ToStatus == "Reopened" && t.RequiredPermission == "Supervisor");
    }
}
