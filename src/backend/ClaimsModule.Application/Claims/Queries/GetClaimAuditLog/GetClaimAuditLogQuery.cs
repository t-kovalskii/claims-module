using ClaimsModule.Application.Common.Models;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;

/// <summary>FRS §10.1 GET /api/claims/{id}/audit: append-only event log, reverse-chronological,
/// paginated (ADR-0002: direct projection).</summary>
public record GetClaimAuditLogQuery(Guid ClaimId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<ClaimAuditLogDto>>;

public record ClaimAuditLogDto(
    Guid Id,
    string EventType,
    string Description,
    string? OldValue,
    string? NewValue,
    Guid? RelatedEntityId,
    string? RelatedEntityType,
    DateTimeOffset CreatedAt,
    Guid? CreatedByUserId);
