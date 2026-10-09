namespace ClaimsModule.Domain.Entities;

/// <summary>The outcome of <see cref="Claim.ApproveReserveTransaction"/> or
/// <see cref="Claim.RejectReserveTransaction"/> — same discriminated shape rationale as
/// <see cref="ReserveRetractionResult"/>. Shared between the two methods since both ever produce
/// exactly the same "allowed with the affected component/history" or "rejected with a message"
/// shapes.</summary>
public class ReserveApprovalActionResult
{
    public bool IsAllowed { get; }
    public string? Message { get; }
    public ReserveComponent? Component { get; }
    public ReserveHistory? History { get; }

    private ReserveApprovalActionResult(bool isAllowed, string? message, ReserveComponent? component, ReserveHistory? history)
    {
        IsAllowed = isAllowed;
        Message = message;
        Component = component;
        History = history;
    }

    public static ReserveApprovalActionResult Allowed(ReserveComponent component, ReserveHistory history) =>
        new(true, null, component, history);

    public static ReserveApprovalActionResult Rejected(string message) => new(false, message, null, null);
}
