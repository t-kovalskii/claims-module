using MediatR;

namespace ClaimsModule.Application.Policies.Queries.GetPolicyCoverage;

/// <summary>FRS §9 / Ticket 03: the coverages seeded for a given policy.</summary>
public record GetPolicyCoverageQuery(Guid PolicyId) : IRequest<List<PolicyCoverageDto>>;

public record PolicyCoverageDto(Guid Id, string CoverageType);
