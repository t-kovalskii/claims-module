using System.Security.Claims;
using ClaimsModule.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ClaimsModule.Infrastructure.Auth;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid? UserId => TryParseGuid(Principal?.FindFirstValue(ClaimTypes.NameIdentifier));

    public Guid? OrganizationId => TryParseGuid(Principal?.FindFirstValue(AppClaimTypes.OrganizationId));

    public string? Username => Principal?.FindFirstValue(ClaimTypes.Name);

    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);

    private static Guid? TryParseGuid(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed : null;
}
