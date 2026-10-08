using System.Linq.Expressions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUserService currentUserService)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<CauseOfLossCode> CauseOfLossCodes => Set<CauseOfLossCode>();
    public DbSet<ClaimStatusTransition> ClaimStatusTransitions => Set<ClaimStatusTransition>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<PolicyCoverage> PolicyCoverages => Set<PolicyCoverage>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<LossEvent> LossEvents => Set<LossEvent>();
    public DbSet<ClaimParty> ClaimParties => Set<ClaimParty>();
    public DbSet<ClaimRiskObject> ClaimRiskObjects => Set<ClaimRiskObject>();
    public DbSet<ClaimAuditLog> ClaimAuditLogs => Set<ClaimAuditLog>();
    public DbSet<ClaimNumberCounter> ClaimNumberCounters => Set<ClaimNumberCounter>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Data model conventions (Ticket 01): every entity added later inherits these for free.
        configurationBuilder.Properties<decimal>().HaveColumnType("decimal(19,4)");
        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("datetimeoffset(7)");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // BaseEntity.DomainEvents is a Domain-internal concern, never persisted (ADR-0003's
        // in-process dispatch reads it straight off the tracked entity, not from the database).
        modelBuilder.Ignore<BaseEvent>();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Row-version concurrency + soft-delete filter for every AuditableEntity.
            if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(AuditableEntity.RowVersion))
                    .IsRowVersion();

                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(BuildSoftDeleteFilter(entityType.ClrType));
            }

            // Organisation isolation: unfiltered when there's no authenticated org yet
            // (e.g. the login lookup itself), scoped once a request is authenticated.
            if (typeof(IHasOrganization).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(BuildOrganizationFilter(entityType.ClrType));
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    private LambdaExpression BuildSoftDeleteFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var property = Expression.Property(parameter, nameof(AuditableEntity.IsDeleted));
        var condition = Expression.Equal(property, Expression.Constant(false));
        return Expression.Lambda(condition, parameter);
    }

    private LambdaExpression BuildOrganizationFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var property = Expression.Property(parameter, nameof(IHasOrganization.OrganizationId));
        var propertyAsNullable = Expression.Convert(property, typeof(Guid?));

        // Compares nullable-to-nullable throughout so the filter never needs Nullable<T>.Value —
        // evaluating that eagerly during EF's partial-evaluation threw even behind the OrElse's
        // intended C#-style short circuit, since expression-tree translation doesn't short-circuit.
        Expression<Func<Guid?>> currentOrgAccessor = () => currentUserService.OrganizationId;
        var currentOrgValue = Expression.Invoke(currentOrgAccessor);

        var noOrgContext = Expression.Equal(currentOrgValue, Expression.Constant(null, typeof(Guid?)));
        var matchesOrg = Expression.Equal(propertyAsNullable, currentOrgValue);
        var condition = Expression.OrElse(noOrgContext, matchesOrg);

        return Expression.Lambda(condition, parameter);
    }
}
