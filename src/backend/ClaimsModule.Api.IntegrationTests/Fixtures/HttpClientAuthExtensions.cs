using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClaimsModule.Application.Auth.Commands.Login;

namespace ClaimsModule.Api.IntegrationTests.Fixtures;

/// <summary>Shared login helper for HTTP-seam tests: log in via the real endpoint and attach
/// the returned token as the client's default Bearer header.</summary>
public static class HttpClientAuthExtensions
{
    public static async Task<string> LoginAsAsync(this HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        var token = result!.Token;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return token;
    }
}
