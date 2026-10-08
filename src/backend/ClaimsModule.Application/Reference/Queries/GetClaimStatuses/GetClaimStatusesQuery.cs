using MediatR;

namespace ClaimsModule.Application.Reference.Queries.GetClaimStatuses;

/// <summary>FRS §4.1/4.2 / Ticket 03: every claim status with its valid next transitions and
/// the permission each requires.</summary>
public record GetClaimStatusesQuery : IRequest<List<ClaimStatusDto>>;

public record ClaimStatusTransitionDto(string ToStatus, string RequiredPermission);

public record ClaimStatusDto(string Status, List<ClaimStatusTransitionDto> ValidNextTransitions);
