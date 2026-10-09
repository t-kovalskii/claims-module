using ClaimsModule.Domain.Entities;
using FluentValidation;

namespace ClaimsModule.Application.Claims.Commands.SubmitReserveTransaction;

/// <summary>
/// BR-R-01 (FRS §7.2/§8): "a reserve transaction amount must be greater than zero (except
/// SubrogationRecoverable, which may be negative)". Classified as FluentValidation/400 rather
/// than a Claim-domain business-rule (422), matching Ticket 04's precedent for BR-C-01/05/07: it
/// depends only on this request's own <see cref="SubmitReserveTransactionCommand.ComponentType"/>
/// and <see cref="SubmitReserveTransactionCommand.Amount"/> fields, never on the claim's loaded
/// state — unlike, say, Ticket 06's reason-required check, which depends on the claim's current
/// status too.
/// </summary>
public class SubmitReserveTransactionCommandValidator : AbstractValidator<SubmitReserveTransactionCommand>
{
    public SubmitReserveTransactionCommandValidator()
    {
        RuleFor(x => x.Amount)
            .Must(IsValidAmount)
            .WithMessage(x => x.ComponentType == ReserveComponentType.SubrogationRecoverable
                ? "SubrogationRecoverable amount must not be zero."
                : "Reserve amount must be greater than zero.");

        // Consistent with the rest of this command's enum-valued fields binding from JSON
        // strings (Program.cs's global JsonStringEnumConverter); an unrecognised string already
        // fails model binding with its own 400 before this validator runs, but this still guards
        // the case where it somehow reaches here as a default/unmapped value.
        RuleFor(x => x.ComponentType)
            .IsInEnum()
            .WithMessage("Invalid reserve component type.");
    }

    private static bool IsValidAmount(SubmitReserveTransactionCommand command, decimal amount) =>
        command.ComponentType == ReserveComponentType.SubrogationRecoverable
            ? amount != 0m
            : amount > 0m;
}
