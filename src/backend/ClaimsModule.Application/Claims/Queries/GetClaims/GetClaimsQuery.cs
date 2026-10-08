using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Entities;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries.GetClaims;

/// <summary>FRS §10.1 GET /api/claims: filterable, paginated list (ADR-0002: direct projection,
/// never the repository).</summary>
public record GetClaimsQuery(
    ClaimStatus? Status,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    Guid? AssignedHandlerUserId,
    string? CauseOfLossCode,
    string? Search,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<ClaimSummaryDto>>;

public record ClaimSummaryDto(
    Guid Id,
    string ClaimNumber,
    string ClientName,
    string? PolicyNumber,
    DateTimeOffset LossDate,
    string Status,
    string CauseOfLossCode,
    decimal TotalReserves);
