using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

/// <summary>
/// Runs the Ticket 03 reference-data SQL script (<c>Scripts/SeedReferenceData.sql</c>) against
/// the database on every app start. The script is embedded in this assembly so execution never
/// depends on output-directory file copying, but it is also a plain, independently re-runnable
/// .sql file on disk — a reviewer can run it directly with sqlcmd against any environment.
///
/// Deliberately a second, parallel seeding mechanism alongside <see cref="ApplicationDbContextSeeder"/>
/// (which seeds <c>Users</c>/<c>Organizations</c> through tracked EF Core entities): spec.md's
/// Implementation Decisions require cause-of-loss codes, status transitions, and simulated
/// policies specifically to come from an independently re-runnable SQL script, not EF Core
/// seed data — the two mechanisms exist for two different kinds of seed data, not as an
/// accidental duplication of the same concern.
/// </summary>
public static class ReferenceDataSeeder
{
    private const string ScriptResourceName = "ClaimsModule.Persistence.Scripts.SeedReferenceData.sql";

    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var assembly = typeof(ReferenceDataSeeder).Assembly;
        using var stream = assembly.GetManifestResourceStream(ScriptResourceName)
            ?? throw new InvalidOperationException($"Embedded seed script \"{ScriptResourceName}\" was not found.");
        using var reader = new StreamReader(stream);
        var script = await reader.ReadToEndAsync(cancellationToken);

        await context.Database.ExecuteSqlRawAsync(script, cancellationToken);
    }
}
