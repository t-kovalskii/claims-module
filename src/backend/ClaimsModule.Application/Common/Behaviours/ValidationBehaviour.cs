using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Common.Behaviours;

public class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var allFailures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        // FRS §5.4: Warning-severity failures are surfaced to the caller but never block the
        // request — only Error-severity (FluentValidation's default) failures do.
        var blockingFailures = allFailures.Where(f => f.Severity == Severity.Error).ToList();
        var warnings = allFailures.Where(f => f.Severity == Severity.Warning).ToList();

        if (blockingFailures.Count != 0)
        {
            throw new Exceptions.ValidationException(blockingFailures);
        }

        var response = await next();

        if (warnings.Count > 0 && response is IHasValidationWarnings responseWithWarnings)
        {
            responseWithWarnings.Warnings.AddRange(warnings.Select(w => w.ErrorMessage));
        }

        return response;
    }
}
