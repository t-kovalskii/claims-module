using System.Reflection;
using ClaimsModule.Application.Claims.Jobs;
using ClaimsModule.Application.Common.Behaviours;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly);

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehaviour<,>));
        });

        // Ticket 09: registered here (not in the Api project) so the job stays reachable from
        // both Hangfire's registration in Program.cs and directly from tests, with no Hangfire
        // dependency of its own.
        services.AddScoped<SlaBreachScanJob>();

        // Ticket 08: same rationale — reachable both from Hangfire's fire-and-forget enqueue
        // (IBackgroundJobScheduler, implemented in ClaimsModule.Infrastructure) and directly from
        // tests exercising its own re-entrancy.
        services.AddScoped<PostGlReserveChangeJob>();

        return services;
    }
}
