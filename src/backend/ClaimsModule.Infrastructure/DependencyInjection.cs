using System.Text;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Infrastructure.Auth;
using ClaimsModule.Infrastructure.Jobs;
using ClaimsModule.Infrastructure.Services;
using ClaimsModule.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClaimsModule.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<StorageSettings>(configuration.GetSection(StorageSettings.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IBackgroundJobScheduler, HangfireBackgroundJobScheduler>();
        AddStorage(services, configuration);

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        // Deferred (not read from `configuration` directly above): WebApplicationFactory-based
        // tests splice their own config in during WebApplicationBuilder.Build(), after this method
        // runs. Binding JwtBearerOptions via IOptions<JwtSettings> here reads the final merged
        // configuration at first resolution, so test overrides reach it — a prior eager
        // `configuration.Get<JwtSettings>()` read the pre-override settings and silently diverged
        // from what JwtTokenGenerator (also IOptions-based) was signing with.
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>>(provider =>
        {
            var jwtSettings = provider.GetRequiredService<IOptions<JwtSettings>>().Value;

            return new ConfigureNamedOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        });

        services.AddAuthorization();

        return services;
    }

    /// <summary>Ticket 10: which <see cref="IStorageService"/> gets registered is a pure
    /// configuration switch (<c>Storage:Provider</c>) — "AzureBlob" in production, "Local" (the
    /// default) for local development, including the fallback case where Azure Blob isn't
    /// reachable locally.</summary>
    private static void AddStorage(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration[$"{StorageSettings.SectionName}:Provider"];

        if (string.Equals(provider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IStorageService, AzureBlobStorageService>();
        }
        else
        {
            services.AddSingleton<LocalFileStorageService>();
            services.AddSingleton<IStorageService>(sp => sp.GetRequiredService<LocalFileStorageService>());
            services.AddSingleton<ILocalSignedDocumentLinkService>(sp => sp.GetRequiredService<LocalFileStorageService>());
        }
    }
}
