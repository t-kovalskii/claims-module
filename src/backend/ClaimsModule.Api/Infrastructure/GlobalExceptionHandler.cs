using ClaimsModule.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ValidationException = ClaimsModule.Application.Common.Exceptions.ValidationException;

namespace ClaimsModule.Api.Infrastructure;

/// <summary>
/// Maps every unhandled exception to an RFC 7807 ProblemDetails response and logs it,
/// so no handler needs its own try/catch and no 500 leaks internals.
/// </summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ValidationException validationException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Extensions = { ["errors"] = validationException.Errors }
            },
            NotFoundException notFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                Detail = notFoundException.Message
            },
            AuthenticationException authenticationException => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication failed.",
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                Detail = authenticationException.Message
            },
            BusinessRuleViolationException businessRuleViolationException =>
                BuildBusinessRuleProblemDetails(businessRuleViolationException),
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            }
        };

        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "{ExceptionType} for {Method} {Path}", exception.GetType().Name, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    /// <summary>Ticket 06 / spec.md: business-rule violations map to 422, with
    /// <see cref="BusinessRuleViolationException.Extensions"/> copied verbatim onto the
    /// ProblemDetails response (e.g. the valid next statuses, or the unmet conditions list).</summary>
    private static ProblemDetails BuildBusinessRuleProblemDetails(BusinessRuleViolationException exception)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "A business rule prevented this operation.",
            Type = "https://tools.ietf.org/html/rfc4918#section-11.2",
            Detail = exception.Message
        };

        foreach (var (key, value) in exception.Extensions)
        {
            problemDetails.Extensions[key] = value;
        }

        return problemDetails;
    }
}
