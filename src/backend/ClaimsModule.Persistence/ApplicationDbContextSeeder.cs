using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

/// <summary>Seeds the three mock-auth users the login endpoint authenticates against.</summary>
public static class ApplicationDbContextSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var organization = await context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Name == "Acme Insurance", cancellationToken);

        if (organization is null)
        {
            organization = new Organization { Name = "Acme Insurance" };
            context.Organizations.Add(organization);
            await context.SaveChangesAsync(cancellationToken);
        }

        var seededUsers = new (string Username, string DisplayName, UserRole Role)[]
        {
            ("handler1", "Hana Handler", UserRole.Handler),
            ("supervisor1", "Sam Supervisor", UserRole.Supervisor),
            ("manager1", "Mika Manager", UserRole.Manager)
        };

        foreach (var (username, displayName, role) in seededUsers)
        {
            var exists = await context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Username == username, cancellationToken);

            if (!exists)
            {
                context.Users.Add(new User
                {
                    Username = username,
                    DisplayName = displayName,
                    Role = role,
                    OrganizationId = organization.Id
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
