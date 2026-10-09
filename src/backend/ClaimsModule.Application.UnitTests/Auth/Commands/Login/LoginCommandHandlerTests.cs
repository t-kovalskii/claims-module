using ClaimsModule.Application.Auth.Commands.Login;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace ClaimsModule.Application.UnitTests.Auth.Commands.Login;

public class LoginCommandHandlerTests
{
    private static readonly Organization Org = new() { Name = "Acme Insurance" };

    private static readonly User Manager = new()
    {
        Username = "manager1",
        DisplayName = "Mika Manager",
        Role = UserRole.Manager,
        OrganizationId = Org.Id
    };

    private static FakeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new FakeDbContext(options);
        context.Organizations.Add(Org);
        context.Users.Add(Manager);
        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task Handle_with_an_unknown_username_throws_AuthenticationException()
    {
        using var context = CreateContext();
        var tokenGenerator = Substitute.For<IJwtTokenGenerator>();
        var sut = new LoginCommandHandler(context, tokenGenerator);

        await Assert.ThrowsAsync<AuthenticationException>(
            () => sut.Handle(new LoginCommand("no-such-user"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_with_a_seeded_username_returns_a_token_and_the_users_role_and_org()
    {
        using var context = CreateContext();
        var tokenGenerator = Substitute.For<IJwtTokenGenerator>();
        tokenGenerator.GenerateToken(Arg.Any<User>()).Returns("fake-jwt");
        var sut = new LoginCommandHandler(context, tokenGenerator);

        var result = await sut.Handle(new LoginCommand("Manager1"), CancellationToken.None);

        Assert.Equal("fake-jwt", result.Token);
        Assert.Equal(Manager.Id, result.UserId);
        Assert.Equal("Manager", result.Role);
        Assert.Equal(Org.Id, result.OrganizationId);
    }
}

/// <summary>Minimal EF Core InMemory stand-in for IApplicationDbContext, scoped to this test file.</summary>
public class FakeDbContext(DbContextOptions<FakeDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<CauseOfLossCode> CauseOfLossCodes => Set<CauseOfLossCode>();
    public DbSet<ClaimStatusTransition> ClaimStatusTransitions => Set<ClaimStatusTransition>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<PolicyCoverage> PolicyCoverages => Set<PolicyCoverage>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimAuditLog> ClaimAuditLogs => Set<ClaimAuditLog>();
    public DbSet<ReserveComponent> ReserveComponents => Set<ReserveComponent>();
    public DbSet<ReserveHistory> ReserveHistory => Set<ReserveHistory>();
    public DbSet<ClaimDocument> ClaimDocuments => Set<ClaimDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<ClaimsModule.Domain.Common.BaseEvent>();
        base.OnModelCreating(modelBuilder);
    }
}
