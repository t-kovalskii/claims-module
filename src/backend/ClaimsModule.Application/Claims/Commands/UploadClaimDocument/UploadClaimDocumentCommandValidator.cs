using FluentValidation;

namespace ClaimsModule.Application.Claims.Commands.UploadClaimDocument;

/// <summary>Request-shape checks only (400) — whether <c>ClaimId</c> refers to a real claim is
/// state-dependent, so (per SubmitReserveTransactionCommandHandler's precedent) that's a 404
/// thrown from the handler instead, not a validator rule.</summary>
public class UploadClaimDocumentCommandValidator : AbstractValidator<UploadClaimDocumentCommand>
{
    public const long MaxSizeBytes = 25 * 1024 * 1024;

    public UploadClaimDocumentCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("A file name is required.");

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .WithMessage("A content type is required.");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0)
            .WithMessage("An empty file cannot be uploaded.")
            .LessThanOrEqualTo(MaxSizeBytes)
            .WithMessage($"Documents may not exceed {MaxSizeBytes / (1024 * 1024)} MB.");
    }
}
