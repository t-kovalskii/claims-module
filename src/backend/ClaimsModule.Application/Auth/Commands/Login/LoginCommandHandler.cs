using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Auth.Commands.Login;

public class LoginCommandHandler(IApplicationDbContext context, IJwtTokenGenerator tokenGenerator)
    : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Both sides use ToLower(), not ToLowerInvariant() — EF Core's SQL Server provider only
        // translates ToLower() to LOWER() for the column side, so the local side matches it.
        var normalizedUsername = request.Username.Trim().ToLower();

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername, cancellationToken);

        if (user is null)
        {
            throw new AuthenticationException($"No seeded user matches username \"{request.Username}\".");
        }

        var token = tokenGenerator.GenerateToken(user);

        return new LoginResult(
            token,
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role.ToString(),
            user.OrganizationId);
    }
}
