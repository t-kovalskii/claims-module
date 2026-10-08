using MediatR;

namespace ClaimsModule.Application.Users.Queries.GetCurrentUser;

public record GetCurrentUserQuery : IRequest<CurrentUserDto>;

public record CurrentUserDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string Role,
    Guid OrganizationId,
    string OrganizationName);
