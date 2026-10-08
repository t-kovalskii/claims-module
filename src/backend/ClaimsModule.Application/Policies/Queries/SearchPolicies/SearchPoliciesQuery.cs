using MediatR;

namespace ClaimsModule.Application.Policies.Queries.SearchPolicies;

/// <summary>FRS §5.2/§9 / Ticket 03: partial match on policy number or client name.</summary>
public record SearchPoliciesQuery(string? Q) : IRequest<List<PolicySummaryDto>>;

public record PolicySummaryDto(
    Guid Id,
    string PolicyNumber,
    string ClientName,
    DateOnly EffectiveDate,
    DateOnly ExpirationDate,
    string Status,
    List<string> CoverageTypes);
