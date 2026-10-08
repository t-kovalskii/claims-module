using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
// Disambiguates against ClaimsModule.Domain.Entities.Claim (the FNOL aggregate root, Ticket
// 04) — this file only ever means the JWT/ClaimsPrincipal kind.
using Claim = System.Security.Claims.Claim;

namespace ClaimsModule.Infrastructure.Auth;

public class JwtTokenGenerator(IOptions<JwtSettings> options) : IJwtTokenGenerator
{
    private readonly JwtSettings _settings = options.Value;

    public string GenerateToken(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(AppClaimTypes.OrganizationId, user.OrganizationId.ToString()),
            new(AppClaimTypes.DisplayName, user.DisplayName)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
