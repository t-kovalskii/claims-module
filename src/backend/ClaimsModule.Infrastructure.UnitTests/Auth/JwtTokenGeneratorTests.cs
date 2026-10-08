using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ClaimsModule.Infrastructure.UnitTests.Auth;

public class JwtTokenGeneratorTests
{
    private static readonly JwtSettings Settings = new()
    {
        SecretKey = "unit-test-signing-key-at-least-32-bytes-long!!",
        Issuer = "ClaimsModule.Tests",
        Audience = "ClaimsModule.Tests.Clients",
        ExpiryMinutes = 60
    };

    private static JwtTokenGenerator CreateSut() => new(Options.Create(Settings));

    private static readonly User Manager = new()
    {
        Id = Guid.NewGuid(),
        Username = "manager1",
        DisplayName = "Pat Manager",
        Role = UserRole.Manager,
        OrganizationId = Guid.NewGuid()
    };

    [Fact]
    public void GenerateToken_produces_a_token_that_validates_against_the_configured_key()
    {
        var sut = CreateSut();

        var token = sut.GenerateToken(Manager);

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidIssuer = Settings.Issuer,
            ValidAudience = Settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Settings.SecretKey)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        }, out _);

        Assert.NotNull(principal);
    }

    [Fact]
    public void GenerateToken_carries_role_and_organization_claims_for_the_given_user()
    {
        var sut = CreateSut();

        var token = sut.GenerateToken(Manager);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(Manager.Id.ToString(), jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(Manager.Role.ToString(), jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(Manager.OrganizationId.ToString(), jwt.Claims.First(c => c.Type == AppClaimTypes.OrganizationId).Value);
    }
}
