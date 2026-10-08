using System.Data.Common;
using ClaimsModule.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

/// <summary>
/// ADR-0008: an atomic, per-organisation, per-year counter incremented via
/// <c>UPDATE ... OUTPUT INSERTED</c>, with an insert-on-first-use fallback for a brand-new
/// (organisation, year) pair. Runs against whatever ambient transaction the caller
/// (CreateClaimCommandHandler, via IUnitOfWork) has already opened on the same IApplicationDbContext,
/// so a failed claim creation rolls the counter increment back too — no gaps, no reuse.
/// </summary>
public class ClaimNumberGenerator(IApplicationDbContext context, IDateTime dateTime) : IClaimNumberGenerator
{
    public async Task<string> GenerateAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var year = dateTime.UtcNow.Year;

        var sequence = await TryIncrementAsync(organizationId, year, cancellationToken);

        if (sequence is null)
        {
            try
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO ClaimNumberCounters (Id, OrganizationId, Year, LastValue)
                     VALUES (NEWID(), {organizationId}, {year}, 1)
                     """,
                    cancellationToken);
                sequence = 1;
            }
            catch (DbException)
            {
                // Lost the race to another concurrent first-submission-of-the-year: that row
                // now exists, so the atomic UPDATE above will succeed on retry.
                sequence = await TryIncrementAsync(organizationId, year, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Failed to generate a claim number for organisation {organizationId}, year {year}.");
            }
        }

        return $"CLM-{year}-{sequence.Value:D7}";
    }

    private async Task<int?> TryIncrementAsync(Guid organizationId, int year, CancellationToken cancellationToken)
    {
        var results = await context.Database.SqlQueryRaw<int>(
            """
            UPDATE ClaimNumberCounters
            SET LastValue = LastValue + 1
            OUTPUT INSERTED.LastValue
            WHERE OrganizationId = {0} AND Year = {1}
            """,
            organizationId, year).ToListAsync(cancellationToken);

        return results.Count > 0 ? results[0] : null;
    }
}
