namespace ClaimsModule.Domain.Entities;

/// <summary>
/// The outcome of <see cref="Claim.ValidateTransition"/> — one of four distinct rejection shapes
/// (invalid from/to pair, insufficient role, unmet business conditions) or allowed, each carrying
/// exactly the structured detail <c>PUT /api/claims/{id}/status</c> needs to return in its 422
/// (BR-ST-01/02/03/04). Ticket 06.
/// </summary>
public class ClaimTransitionValidationResult
{
    public bool IsAllowed { get; }
    public string? Message { get; }
    public IReadOnlyList<ClaimStatus>? ValidNextStatuses { get; }
    public UserRole? RequiredRole { get; }
    public IReadOnlyList<string>? UnmetConditions { get; }

    private ClaimTransitionValidationResult(
        bool isAllowed,
        string? message,
        IReadOnlyList<ClaimStatus>? validNextStatuses,
        UserRole? requiredRole,
        IReadOnlyList<string>? unmetConditions)
    {
        IsAllowed = isAllowed;
        Message = message;
        ValidNextStatuses = validNextStatuses;
        RequiredRole = requiredRole;
        UnmetConditions = unmetConditions;
    }

    public static ClaimTransitionValidationResult Allowed() => new(true, null, null, null, null);

    public static ClaimTransitionValidationResult InvalidTransition(string message, IReadOnlyList<ClaimStatus> validNextStatuses) =>
        new(false, message, validNextStatuses, null, null);

    public static ClaimTransitionValidationResult InsufficientRole(string message, UserRole requiredRole) =>
        new(false, message, null, requiredRole, null);

    public static ClaimTransitionValidationResult UnmetBusinessConditions(string message, IReadOnlyList<string> unmetConditions) =>
        new(false, message, null, null, unmetConditions);
}
