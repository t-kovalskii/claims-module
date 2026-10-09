namespace ClaimsModule.Domain.Entities;

/// <summary>
/// The outcome of <see cref="Claim.SubmitReserveTransaction"/> — mirrors
/// <see cref="ClaimTransitionValidationResult"/>'s allowed/rejected discriminated shape (Ticket 06
/// precedent). Unlike that type, there's exactly one rejection shape here (a message only) since
/// neither of this method's business-rule failures need structured extension data.
/// </summary>
public class ReserveSubmissionResult
{
    public bool IsAllowed { get; }
    public string? Message { get; }
    public ReserveComponent? Component { get; }
    public ReserveHistory? History { get; }

    private ReserveSubmissionResult(bool isAllowed, string? message, ReserveComponent? component, ReserveHistory? history)
    {
        IsAllowed = isAllowed;
        Message = message;
        Component = component;
        History = history;
    }

    public static ReserveSubmissionResult Allowed(ReserveComponent component, ReserveHistory history) =>
        new(true, null, component, history);

    public static ReserveSubmissionResult Rejected(string message) => new(false, message, null, null);
}
