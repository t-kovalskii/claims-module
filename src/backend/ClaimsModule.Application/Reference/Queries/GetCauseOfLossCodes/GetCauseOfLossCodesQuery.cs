using ClaimsModule.Domain.Entities;
using MediatR;

namespace ClaimsModule.Application.Reference.Queries.GetCauseOfLossCodes;

/// <summary>FRS §5.6 / Ticket 03: only active codes, optionally filtered by peril category.</summary>
public record GetCauseOfLossCodesQuery(PerilCategory? PerilCategory) : IRequest<List<CauseOfLossCodeDto>>;

public record CauseOfLossCodeDto(
    Guid Id,
    string Code,
    string Name,
    string PerilCategory,
    int SortOrder);
