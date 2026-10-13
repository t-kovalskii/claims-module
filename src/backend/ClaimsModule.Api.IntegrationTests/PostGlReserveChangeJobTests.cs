using System.Net.Http.Json;
using ClaimsModule.Api.IntegrationTests.Fixtures;
using ClaimsModule.Application.Claims.Jobs;
using ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;
using ClaimsModule.Application.Claims.Queries.GetClaimById;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Application.Claims.Commands.SubmitReserveTransaction;
using ClaimsModule.Application.Policies.Queries.SearchPolicies;
using ClaimsModule.Application.Reference.Queries.GetCauseOfLossCodes;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClaimsModule.Api.IntegrationTests;

/// <summary>
/// Ticket 08's GL posting simulation job (BR-R-06 / FRS §12.1): per spec.md's Testing Decisions,
/// background jobs are covered by the primary HTTP seam, not a separate one — the "running it
/// twice produces exactly one audit entry and one posting-status update" acceptance criterion is
/// exercised here by invoking PostGlReserveChangeJob.ExecuteAsync directly (same pattern as
/// SlaBreachScanJobTests), rather than waiting on or faking Hangfire's own fire-and-forget
/// dispatch — the dispatch itself is covered by ReservesTests' polling assertions instead.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class PostGlReserveChangeJobTests(ApiWebApplicationFactory factory)
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

    private async Task<Guid> CreateClaimWithAutoApprovedReserveAsync()
    {
        var causeId = await GetActiveCauseOfLossCodeIdAsync();
        var policyId = await GetActivePolicyIdAsync();
        var response = await _client.PostAsJsonAsync("/api/claims", new
        {
            policyId,
            lossEvent = new
            {
                lossDate = DateTimeOffset.UtcNow.AddDays(-2),
                lossDescription = "A warehouse sprinkler malfunction damaged stored inventory.",
                lossLocation = "88 Dock Street, Portside",
                causeOfLossCodeId = causeId,
                estimatedLossAmount = 8000m,
                policeReportNumber = (string?)null
            },
            assignedHandlerUserId = (Guid?)null,
            parties = new[] { new { role = "Claimant", partyType = "Person", firstName = "Sam", lastName = "Lee" } },
            riskObjects = Array.Empty<object>()
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateClaimResponseDto>();
        return created!.Id;
    }

    /// <summary>Submits an Indemnity reserve ≤ $10,000 so it's AutoApproved — the job's own
    /// idempotency check (PostingStatus) is what this test exercises, not the enqueue path, so a
    /// manually-held-back history row (never actually enqueued through Hangfire) is enough; the
    /// job is invoked directly below regardless of what SubmitReserveTransactionCommandHandler's
    /// own real enqueue did or didn't manage to run in the background.</summary>
    private async Task<Guid> SubmitAutoApprovedReserveAsync(Guid claimId)
    {
        var response = await _client.PostAsJsonAsync($"/api/claims/{claimId}/reserves",
            new { componentType = "Indemnity", amount = 5000m, changeReason = "Initial reserve" });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SubmitReserveTransactionResult>();
        return result!.ReserveHistoryId;
    }

    private static async Task RunJobAsync(ApiWebApplicationFactory factory, Guid reserveHistoryId)
    {
        using var scope = factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<PostGlReserveChangeJob>();
        await job.ExecuteAsync(reserveHistoryId);
    }

    [Fact]
    public async Task ExecuteAsync_posts_the_simulated_entry_and_marks_the_transaction_Posted()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimWithAutoApprovedReserveAsync();
        var historyId = await SubmitAutoApprovedReserveAsync(claimId);

        await RunJobAsync(factory, historyId);

        var auditResponse = await _client.GetAsync($"/api/claims/{claimId}/audit");
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.Contains(audit!.Items, a => a.EventType == "GL_POSTING_SIMULATED" && a.RelatedEntityId == historyId);

        var detail = await _client.GetAsync($"/api/claims/{claimId}");
        var claim = await detail.Content.ReadFromJsonAsync<ClaimDetailDto>();
        var entry = Assert.Single(Assert.Single(claim!.Reserves).History);
        Assert.Equal("Posted", entry.PostingStatus);
    }

    [Fact]
    public async Task ExecuteAsync_run_twice_produces_exactly_one_audit_entry_and_one_status_update()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimWithAutoApprovedReserveAsync();
        var historyId = await SubmitAutoApprovedReserveAsync(claimId);

        await RunJobAsync(factory, historyId);
        await RunJobAsync(factory, historyId); // re-run immediately — must be a no-op

        var auditResponse = await _client.GetAsync($"/api/claims/{claimId}/audit");
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResult<ClaimAuditLogDto>>();
        Assert.Single(audit!.Items, a => a.EventType == "GL_POSTING_SIMULATED" && a.RelatedEntityId == historyId);

        var detail = await _client.GetAsync($"/api/claims/{claimId}");
        var claim = await detail.Content.ReadFromJsonAsync<ClaimDetailDto>();
        var entry = Assert.Single(Assert.Single(claim!.Reserves).History);
        Assert.Equal("Posted", entry.PostingStatus);
    }

    [Fact]
    public async Task ExecuteAsync_for_an_unknown_reserve_history_id_is_a_no_op()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var auditCountBefore = await context.ClaimAuditLogs.CountAsync();

        await RunJobAsync(factory, Guid.NewGuid());

        var auditCountAfter = await context.ClaimAuditLogs.CountAsync();
        Assert.Equal(auditCountBefore, auditCountAfter);
    }

    private record CreateClaimResponseDto(Guid Id, string ClaimNumber, string Status, List<string> Warnings);
}
