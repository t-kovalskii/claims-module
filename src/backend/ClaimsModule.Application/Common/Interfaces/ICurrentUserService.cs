namespace ClaimsModule.Application.Common.Interfaces;

/// <summary>
/// Reads identity claims off the current request. Returns null for everything when there is
/// no authenticated user yet (e.g. the login request itself), rather than throwing.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? OrganizationId { get; }
    string? Username { get; }
    string? Role { get; }
}
