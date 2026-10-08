using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;
using Xunit;

namespace ClaimsModule.Api.IntegrationTests.Fixtures;

/// <summary>
/// Boots the real API against a disposable SQL Server container, so these tests exercise the
/// same migration + seeding + auth pipeline a developer gets by running the app locally.
/// </summary>
public class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public async Task InitializeAsync() => await _sqlContainer.StartAsync();

    public new async Task DisposeAsync() => await _sqlContainer.DisposeAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ClaimsModuleDb"] = $"{_sqlContainer.GetConnectionString()};TrustServerCertificate=True",
                ["Jwt:SecretKey"] = "integration-test-signing-key-at-least-32-bytes-long!!",
                ["Jwt:Issuer"] = "ClaimsModule.Api.Tests",
                ["Jwt:Audience"] = "ClaimsModule.Clients.Tests"
            });
        });
    }
}
