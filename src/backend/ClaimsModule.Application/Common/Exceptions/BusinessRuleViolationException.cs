namespace ClaimsModule.Application.Common.Exceptions;

/// <summary>
/// spec.md's error-handling convention: business-rule violations (state-dependent workflow
/// rules, as opposed to FluentValidation's request-shape checks, which map to 400 via
/// <see cref="ValidationException"/>) map to 422 ProblemDetails. First used by Ticket 06's status
/// transitions (invalid from/to pair, insufficient role, unmet Open/Closed/Reopen conditions);
/// <see cref="Extensions"/> carries whatever structured detail the specific rule needs to return
/// (e.g. the valid next statuses, or the unmet conditions list).
/// </summary>
public class BusinessRuleViolationException(string message, IDictionary<string, object>? extensions = null)
    : Exception(message)
{
    public IDictionary<string, object> Extensions { get; } = extensions ?? new Dictionary<string, object>();
}
