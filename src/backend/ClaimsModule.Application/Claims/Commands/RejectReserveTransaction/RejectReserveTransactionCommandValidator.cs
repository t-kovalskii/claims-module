using FluentValidation;

namespace ClaimsModule.Application.Claims.Commands.RejectReserveTransaction;

/// <summary>FRS §10.2: "Body: { rejectionReason }" — required, same 400-vs-422 classification
/// rationale as SubmitReserveTransactionCommandValidator: this depends only on the request's own
/// field, never on the claim's loaded state.</summary>
public class RejectReserveTransactionCommandValidator : AbstractValidator<RejectReserveTransactionCommand>
{
    public RejectReserveTransactionCommandValidator()
    {
        RuleFor(x => x.RejectionReason).NotEmpty().WithMessage("A rejection reason is required.");
    }
}
