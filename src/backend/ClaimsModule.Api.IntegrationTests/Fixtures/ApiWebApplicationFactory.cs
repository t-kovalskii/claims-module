using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
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
        // Even past this log line, the container's published port hasn't reliably come up yet —
        // observed locally (Apple Silicon, under Docker's x64 emulation) as a TCP-level connection
        // failure from the very first migration attempt despite this message already having been
        // logged. WaitForTcpConnectionAsync below is the actual readiness gate; this wait strategy
        // just avoids hammering the container with connection attempts during its ~40-70s startup.
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("SQL Server is now ready for client connections"))
        .Build();

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        await WaitForTcpConnectionAsync();
    }

    public new async Task DisposeAsync() => await _sqlContainer.DisposeAsync();

    /// <summary>Belt-and-suspenders on top of the container's own wait strategy: retries an actual
    /// ADO.NET connection against the published port until it succeeds, since the log-line signal
    /// above has been observed to fire before Docker's host-side port mapping is reliably up.</summary>
    private async Task WaitForTcpConnectionAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var connection = new SqlConnection($"{_sqlContainer.GetConnectionString()};TrustServerCertificate=True;Connect Timeout=5");
                await connection.OpenAsync();
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        throw new TimeoutException(
            "SQL Server container did not accept a connection within 90 seconds of starting.", lastError);
    }

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
