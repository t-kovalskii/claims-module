using System.Net.Http.Json;
using ClaimsModule.Api.IntegrationTests.Fixtures;
using ClaimsModule.Application.Claims.Jobs;
using ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;
using ClaimsModule.Application.Claims.Queries.GetClaimById;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Application.Reference.Queries.GetCauseOfLossCodes;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClaimsModule.Api.IntegrationTests;

/// <summary>
/// Ticket 09's recurring SLA breach scan. Per spec.md's Testing Decisions, background jobs are
/// covered by the primary HTTP seam, not a separate one — these tests create real claims through
/// the API, manipulate their timestamps directly in the database (bypassing
/// AuditableEntitySaveChangesInterceptor, which would otherwise stamp LastModifiedAt back to
/// "now" the moment EF sees the row as Modified), then invoke SlaBreachScanJob.ExecuteAsync
/// directly — exactly as the ticket requires, without waiting on or faking Hangfire's own
/// 15-minute schedule.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class SlaBreachScanJobTests(ApiWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> GetActiveCauseOfLossCodeIdAsync()
    {
        var response = await _client.GetAsync("/api/reference/cause-of-loss-codes");
        var codes = await response.Content.ReadFromJsonAsync<List<CauseOfLossCodeDto>>();
        return codes!.Single(c => c.Code == "COL-FIRE").Id;
    }

    private async Task<Guid> CreateClaimAsync(Guid causeOfLossCodeId)
    {
        var body = new
        {
            policyId = (Guid?)null,
            lossEvent = new
            {
                lossDate = DateTimeOffset.UtcNow.AddDays(-3),
                lossDescription = "A delivery van collided with the loading dock, causing structural damage.",
                lossLocation = "123 Harbor Way, Portside",
                causeOfLossCodeId,
                estimatedLossAmount = 15000m,
                policeReportNumber = (string?)null
            },
            assignedHandlerUserId = (Guid?)null,
            parties = new[]
            {
                new { role = "Claimant", partyType = "Person", firstName = "Jane", lastName = "Doe" }
            },
            riskObjects = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/api/claims", body);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateClaimResponseDto>();
        return created!.Id;
    }

    /// <summary>Directly rewrites the row's audit-column timestamps via raw SQL, bypassing EF's
    /// change tracking entirely — going through SaveChanges here would hit
    /// AuditableEntitySaveChangesInterceptor, which stamps LastModifiedAt to the real current
    /// time for any Modified entity and would silently undo the simulated staleness.</summary>
    private static async Task SetClaimTimestampsAsync(ApiWebApplicationFactory factory, Guid claimId, DateTimeOffset asOf)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Claims SET CreatedAt = {asOf}, LastModifiedAt = {asOf} WHERE Id = {claimId}");
    }

    private static async Task SetAuditEntryCreatedAtAsync(ApiWebApplicationFactory factory, Guid claimId, string eventType, DateTimeOffset asOf)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ClaimAuditLogs SET CreatedAt = {asOf} WHERE ClaimId = {claimId} AND EventType = {eventType}");
    }

    private static async Task RunJobAsync(ApiWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<SlaBreachScanJob>();
        await job.ExecuteAsync();
    }

    [Fact]
    public async Task Flags_a_draft_claim_with_no_update_in_48_plus_hours()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();
        var claimId = await CreateClaimAsync(causeId);
        await SetClaimTimestampsAsync(factory, claimId, DateTimeOffset.UtcNow.AddHours(-50));

        await RunJobAsync(factory);

        var auditResponse = await _client.GetAsync($"/api/claims/{claimId}/audit");
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.Contains(audit!.Items, a => a.EventType == "SLA_BREACH_DETECTED");
    }

    [Fact]
    public async Task Does_not_flag_a_claim_updated_within_the_last_48_hours()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();
        var claimId = await CreateClaimAsync(causeId); // fresh — CreatedAt is "now"

        await RunJobAsync(factory);

        var auditResponse = await _client.GetAsync($"/api/claims/{claimId}/audit");
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.DoesNotContain(audit!.Items, a => a.EventType == "SLA_BREACH_DETECTED");
    }

    [Fact]
    public async Task Running_the_job_twice_within_24_hours_does_not_duplicate_the_breach_entry()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();
        var claimId = await CreateClaimAsync(causeId);
        await SetClaimTimestampsAsync(factory, claimId, DateTimeOffset.UtcNow.AddHours(-50));

        await RunJobAsync(factory);
        await RunJobAsync(factory); // re-run immediately, well within the 24h throttle window

        var auditResponse = await _client.GetAsync($"/api/claims/{claimId}/audit");
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.Single(audit!.Items, a => a.EventType == "SLA_BREACH_DETECTED");
    }

    [Fact]
    public async Task Re_flags_a_still_stale_claim_once_the_24_hour_throttle_window_has_passed()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();
        var claimId = await CreateClaimAsync(causeId);
        await SetClaimTimestampsAsync(factory, claimId, DateTimeOffset.UtcNow.AddHours(-50));

        await RunJobAsync(factory);
        // Push the existing breach entry's CreatedAt outside the 24h throttle window so a
        // second run is allowed to add a new one for the same ongoing breach.
        await SetAuditEntryCreatedAtAsync(factory, claimId, "SLA_BREACH_DETECTED", DateTimeOffset.UtcNow.AddHours(-25));
        await RunJobAsync(factory);

        var auditResponse = await _client.GetAsync($"/api/claims/{claimId}/audit");
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.Equal(2, audit!.Items.Count(a => a.EventType == "SLA_BREACH_DETECTED"));
    }

    [Fact]
    public async Task Never_changes_the_claims_status_even_when_flagged()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();
        var claimId = await CreateClaimAsync(causeId);
        await SetClaimTimestampsAsync(factory, claimId, DateTimeOffset.UtcNow.AddHours(-50));

        await RunJobAsync(factory);

        var detailResponse = await _client.GetAsync($"/api/claims/{claimId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ClaimDetailDto>();
        Assert.Equal("Draft", detail!.Status);
    }

    [Fact]
    public async Task Does_not_flag_a_stale_claim_outside_draft_or_open_status()
    {
        await _client.LoginAsAsync("handler1");
        var causeId = await GetActiveCauseOfLossCodeIdAsync();
        var claimId = await CreateClaimAsync(causeId);
        await SetClaimTimestampsAsync(factory, claimId, DateTimeOffset.UtcNow.AddHours(-50));

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Claims SET Status = 'Withdrawn' WHERE Id = {claimId}");
        }

        await RunJobAsync(factory);

        var auditResponse = await _client.GetAsync($"/api/claims/{claimId}/audit");
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.DoesNotContain(audit!.Items, a => a.EventType == "SLA_BREACH_DETECTED");
    }

    /// <summary>Mirrors CreateClaimResult's shape without taking a dependency on the
    /// Application layer's internal command type, same as ClaimsTests.cs's own copy.</summary>
    private record CreateClaimResponseDto(Guid Id, string ClaimNumber, string Status, List<string> Warnings);
}
