using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Users.Queries.GetCurrentUser;

public class GetCurrentUserQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    : IRequestHandler<GetCurrentUserQuery, CurrentUserDto>
{
    public async Task<CurrentUserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw new AuthenticationException("No authenticated user on the current request.");

        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Organization)
            .Where(u => u.Id == userId)
            .Select(u => new CurrentUserDto(
                u.Id,
                u.Username,
                u.DisplayName,
                u.Role.ToString(),
                u.OrganizationId,
                u.Organization!.Name))
            .FirstOrDefaultAsync(cancellationToken);

        return user ?? throw new NotFoundException(nameof(Domain.Entities.User), userId);
    }
}
