using ClaimsModule.Application.Common.Interfaces;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Commands.CreateClaim;

/// <summary>
/// FRS §5.4 / §8 "Validation Rules" + this ticket's checklist. Rules default to
/// <see cref="Severity.Error"/> (Critical, blocking); rules explicitly marked
/// <see cref="Severity.Warning"/> never block (enforced by ValidationBehaviour).
/// </summary>
public class CreateClaimCommandValidator : AbstractValidator<CreateClaimCommand>
{
    public CreateClaimCommandValidator(IApplicationDbContext context, IDateTime dateTime)
    {
        RuleFor(x => x.LossEvent.LossDate)
            .NotEqual(default(DateTimeOffset))
            .WithMessage("Loss date is required.");

        RuleFor(x => x.LossEvent.LossDate)
            .LessThanOrEqualTo(_ => dateTime.UtcNow)
            .WithMessage("Loss date cannot be in the future.");

        RuleFor(x => x.LossEvent.LossDescription)
            .NotEmpty()
            .WithMessage("Loss description is required and must be at least 20 characters.")
            .MinimumLength(20)
            .WithMessage("Loss description is required and must be at least 20 characters.");

        RuleFor(x => x.LossEvent.CauseOfLossCodeId)
            .MustAsync((id, ct) => context.CauseOfLossCodes.AnyAsync(c => c.Id == id && c.IsActive, ct))
            .WithMessage("Cause of loss code is not recognised or is inactive.");

        // BR-C-06 / FRS §8: no policy linked is a non-blocking Warning, not a rejection.
        RuleFor(x => x.PolicyId)
            .Must(id => id.HasValue)
            .WithSeverity(Severity.Warning)
            .WithMessage("No policy linked. Policy must be associated before reserves can be set.");

        // One lookup covers both the (defensive, Critical) "policy exists" check and BR-C-02's
        // (Warning) loss-date-vs-effective-period check, rather than two separate MustAsync
        // rules each re-querying Policies by Id.
        RuleFor(x => x)
            .CustomAsync(async (command, validationContext, cancellationToken) =>
            {
                if (command.PolicyId is null)
                {
                    return;
                }

                var policy = await context.Policies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == command.PolicyId, cancellationToken);

                if (policy is null)
                {
                    // Defensive: PolicyId is a real foreign key — without this, SaveChanges
                    // would surface a raw FK-violation 500 instead of a clean 400. Not an
                    // FRS-listed rule, but needed given the FK.
                    validationContext.AddFailure(
                        new ValidationFailure(nameof(command.PolicyId), "Linked policy was not found."));
                    return;
                }

                var lossDate = DateOnly.FromDateTime(command.LossEvent.LossDate.UtcDateTime);
                if (lossDate < policy.EffectiveDate || lossDate > policy.ExpirationDate)
                {
                    validationContext.AddFailure(new ValidationFailure(
                        nameof(command.LossEvent),
                        "Loss date is outside the policy effective period.")
                    {
                        Severity = Severity.Warning
                    });
                }
            });

        RuleForEach(x => x.RiskObjects)
            .ChildRules(riskObject => riskObject.RuleFor(r => r.AssetDescription)
                .NotEmpty()
                .WithMessage("Risk object asset description is required."));
    }
}
