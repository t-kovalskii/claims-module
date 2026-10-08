using ClaimsModule.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Application.Common.Behaviours;

public class LoggingBehaviour<TRequest, TResponse>(
    ILogger<LoggingBehaviour<TRequest, TResponse>> logger,
    ICurrentUserService currentUserService)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        logger.LogInformation(
            "Handling {RequestName} for user {UserId} in organisation {OrganizationId}",
            requestName,
            currentUserService.UserId,
            currentUserService.OrganizationId);

        try
        {
            var response = await next();

            logger.LogInformation("Handled {RequestName}", requestName);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed handling {RequestName}", requestName);
            throw;
        }
    }
}
