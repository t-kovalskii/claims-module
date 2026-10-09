namespace ClaimsModule.Domain.Entities;

/// <summary>The outcome of <see cref="Claim.RetractReserveTransaction"/> — same discriminated
/// shape rationale as <see cref="ReserveSubmissionResult"/>.</summary>
public class ReserveRetractionResult
{
    public bool IsAllowed { get; }
    public string? Message { get; }
    public ReserveComponent? Component { get; }

    private ReserveRetractionResult(bool isAllowed, string? message, ReserveComponent? component)
    {
        IsAllowed = isAllowed;
        Message = message;
        Component = component;
    }

    public static ReserveRetractionResult Allowed(ReserveComponent component) => new(true, null, component);

    public static ReserveRetractionResult Rejected(string message) => new(false, message, null);
}
