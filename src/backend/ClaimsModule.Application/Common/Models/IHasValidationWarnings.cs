namespace ClaimsModule.Application.Common.Models;

/// <summary>
/// Opt-in seam for a command/query response that can carry non-blocking FluentValidation
/// warnings (<see cref="FluentValidation.Severity.Warning"/>), surfaced by
/// <see cref="Behaviours.ValidationBehaviour{TRequest,TResponse}"/> instead of being thrown as
/// a blocking error. FRS §5.4: Warning-severity issues never block the request they're raised
/// against — e.g. "no policy linked" or "loss date outside the policy effective period".
/// </summary>
public interface IHasValidationWarnings
{
    List<string> Warnings { get; }
}
