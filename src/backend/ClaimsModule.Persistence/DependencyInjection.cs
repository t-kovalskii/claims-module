using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Persistence.Interceptors;
using ClaimsModule.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Persistence;

public static class DependencyInjection
{
    public const string ConnectionStringName = "ClaimsModuleDb";

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Missing required connection string \"{ConnectionStringName}\".");

        // Dispatch registered before the audit-stamping interceptor: any entity a domain-event
        // handler adds to the context (e.g. a ClaimAuditLog row) still needs its CreatedAt/
        // CreatedBy stamped by AuditableEntitySaveChangesInterceptor before the save proceeds.
        services.AddScoped<DispatchDomainEventsInterceptor>();
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseSqlServer(connectionString, sql =>
                    sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
                .AddInterceptors(
                    provider.GetRequiredService<DispatchDomainEventsInterceptor>(),
                    provider.GetRequiredService<AuditableEntitySaveChangesInterceptor>()));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.AddScoped<IClaimNumberGenerator, ClaimNumberGenerator>();

        return services;
    }
}
