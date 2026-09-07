using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TodoApp.Application.DTOs;
using Xunit;

namespace TodoApp.IntegrationTests;

public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterAndLogin_Flow_Succeeds()
    {
        // 1. REGISTER
        var email = $"user_{Guid.NewGuid():N}@example.com";
        var password = "Password123!";

        var registerRequest = new RegisterRequest
        {
            Email = email,
            Password = password
        };

        var registerResponse = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var authResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(authResult);
        Assert.False(string.IsNullOrWhiteSpace(authResult.Token));
        Assert.False(string.IsNullOrWhiteSpace(authResult.RefreshToken));
        Assert.Equal(email, authResult.Email);

        // 2. LOGIN
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(loginResult);
        Assert.False(string.IsNullOrWhiteSpace(loginResult.Token));

        // 3. REFRESH TOKEN
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = loginResult.RefreshToken
        };

        var refreshResponse = await _client.PostAsJsonAsync("/api/Auth/refresh", refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshResult = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(refreshResult);
        Assert.NotEqual(loginResult.RefreshToken, refreshResult.RefreshToken); // Token rotasyonu

        // 4. CHANGE PASSWORD
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", refreshResult.Token);

        var changePasswordRequest = new ChangePasswordRequest
        {
            CurrentPassword = password,
            NewPassword = "NewPassword123!"
        };

        var changePasswordResponse = await _client.PutAsJsonAsync("/api/Auth/change-password", changePasswordRequest);
        Assert.Equal(HttpStatusCode.NoContent, changePasswordResponse.StatusCode);

        // 5. LOGOUT
        var logoutRequest = new RefreshTokenRequest
        {
            RefreshToken = refreshResult.RefreshToken
        };

        var logoutResponse = await _client.PostAsJsonAsync("/api/Auth/logout", logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
    }

    [Fact]
    public async Task Register_WhenDuplicateEmail_ReturnsConflict()
    {
        var email = $"duplicate_{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest { Email = email, Password = "Password123!" };

        var firstResponse = await _client.PostAsJsonAsync("/api/Auth/register", request);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await _client.PostAsJsonAsync("/api/Auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }
}
