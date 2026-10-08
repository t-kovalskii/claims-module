using MediatR;

namespace ClaimsModule.Application.Auth.Commands.Login;

public record LoginCommand(string Username) : IRequest<LoginResult>;

public record LoginResult(
    string Token,
    Guid UserId,
    string Username,
    string DisplayName,
    string Role,
    Guid OrganizationId);
