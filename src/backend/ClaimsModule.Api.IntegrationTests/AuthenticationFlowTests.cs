using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Api.IntegrationTests.Fixtures;
using ClaimsModule.Application.Auth.Commands.Login;
using ClaimsModule.Application.Users.Queries.GetCurrentUser;
using Xunit;

namespace ClaimsModule.Api.IntegrationTests;

/// <summary>
/// Ticket 01's acceptance criteria, exercised end-to-end: login via the real login endpoint,
/// then call a protected endpoint with the returned token.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class AuthenticationFlowTests(ApiWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_with_a_seeded_username_returns_200_and_a_token()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { username = "manager1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        Assert.False(string.IsNullOrWhiteSpace(result!.Token));
        Assert.Equal("Manager", result.Role);
    }

    [Fact]
    public async Task Login_with_an_unknown_username_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { username = "no-such-user" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_an_empty_username_returns_400_with_validation_errors()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { username = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_with_a_valid_token_returns_the_authenticated_user()
    {
        await _client.LoginAsAsync("handler1");

        var response = await _client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        Assert.Equal("handler1", user!.Username);
        Assert.Equal("Handler", user.Role);
    }

    [Fact]
    public async Task Role_restricted_endpoint_rejects_a_non_manager_token()
    {
        await _client.LoginAsAsync("handler1");

        var response = await _client.GetAsync("/api/users/admin-only");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Role_restricted_endpoint_accepts_a_manager_token()
    {
        await _client.LoginAsAsync("manager1");

        var response = await _client.GetAsync("/api/users/admin-only");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
