using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TodoApp.Application.Common;
using TodoApp.Application.DTOs;
using TodoApp.Infrastructure.Data;
using Xunit;

namespace TodoApp.IntegrationTests;

public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
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
        Assert.False(string.IsNullOrWhiteSpace(authResult.RefreshToken)); // Refresh token body'de dönmeli
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
        Assert.False(string.IsNullOrWhiteSpace(loginResult.RefreshToken));

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
        Assert.False(string.IsNullOrWhiteSpace(refreshResult.Token));
        Assert.False(string.IsNullOrWhiteSpace(refreshResult.RefreshToken));
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

    [Fact]
    public async Task RefreshToken_IsStoredAsSha256HashInDatabase()
    {
        // 1. REGISTER
        var email = $"token_hash_{Guid.NewGuid():N}@example.com";
        var password = "Password123!";
        var registerRequest = new RegisterRequest { Email = email, Password = password };

        var registerResponse = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var authResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(authResult);
        var plainRefreshToken = authResult.RefreshToken;
        Assert.False(string.IsNullOrWhiteSpace(plainRefreshToken));

        // 2. DB'DEKİ TOKENI KONTROL ET
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbToken = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.UserId == authResult.UserId);

        Assert.NotNull(dbToken);
        // Veritabanında düz metin saklanmamalı (T8.1.7)
        Assert.NotEqual(plainRefreshToken, dbToken.Token);
        // Veritabanındaki değer, düz metnin SHA-256 hash'i olmalı
        Assert.Equal(TokenHelper.HashToken(plainRefreshToken), dbToken.Token);

        // 3. REFRESH İŞLEMİNİN DÜZ METİN TOKEN İLE ÇALIŞTIĞINI DOĞRULA
        var refreshResponse = await _client.PostAsJsonAsync("/api/Auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = plainRefreshToken
        });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ReturnsBadRequest()
    {
        var refreshResponse = await _client.PostAsJsonAsync("/api/Auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = "invalid-token-value"
        });
        Assert.Equal(HttpStatusCode.BadRequest, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task TwoFactorAuthentication_FullFlow_Succeeds_And_EnforcesSecurity()
    {
        // 1. REGISTER
        var email = $"2fa_flow_{Guid.NewGuid():N}@example.com";
        var password = "Password123!";
        var registerResponse = await _client.PostAsJsonAsync("/api/Auth/register", new RegisterRequest
        {
            Email = email,
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var regAuth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(regAuth);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regAuth.Token);

        // 2. ENABLE 2FA
        var enableResponse = await _client.PostAsync("/api/Auth/2fa/enable", null);
        Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);

        var enableResult = await enableResponse.Content.ReadFromJsonAsync<TwoFactorEnableResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(enableResult);
        Assert.False(string.IsNullOrEmpty(enableResult.Secret));

        // 3. VERIFY 2FA SETUP
        var totp = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(enableResult.Secret));
        var setupCode = totp.ComputeTotp();

        var verifyResponse = await _client.PostAsJsonAsync("/api/Auth/2fa/verify", new TwoFactorVerifyRequest
        {
            Code = setupCode
        });
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        // 4. ATTEMPT LOGIN WITH PASSWORD -> REQUIRES 2FA & RETURNS TEMP TOKEN
        _client.DefaultRequestHeaders.Authorization = null;
        var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(loginResult);
        Assert.True(loginResult.RequiresTwoFactor);
        Assert.False(string.IsNullOrWhiteSpace(loginResult.TwoFactorToken));
        Assert.True(string.IsNullOrEmpty(loginResult.Token)); // Parola sonrası tam token verilmemeli

        // 5. SECURITY CHECK: TEMP TOKEN CANNOT BE USED AS ACCESS TOKEN
        var tempClient = _factory.CreateClient();
        tempClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.TwoFactorToken);
        var accessCheckResponse = await tempClient.GetAsync("/api/TodoLists");
        Assert.Equal(HttpStatusCode.Unauthorized, accessCheckResponse.StatusCode);

        // 6. SECURITY CHECK: INVALID TEMP TOKEN ON LOGIN-2FA IS REJECTED
        var invalid2FaResponse = await _client.PostAsJsonAsync("/api/Auth/login-2fa", new TwoFactorLoginRequest
        {
            TwoFactorToken = "tampered-fake-token",
            Code = totp.ComputeTotp()
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid2FaResponse.StatusCode);

        // 7. SUCCESSFUL 2FA LOGIN
        var valid2FaResponse = await _client.PostAsJsonAsync("/api/Auth/login-2fa", new TwoFactorLoginRequest
        {
            TwoFactorToken = loginResult.TwoFactorToken,
            Code = totp.ComputeTotp()
        });
        Assert.Equal(HttpStatusCode.OK, valid2FaResponse.StatusCode);

        var finalAuth = await valid2FaResponse.Content.ReadFromJsonAsync<AuthResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(finalAuth);
        Assert.False(finalAuth.RequiresTwoFactor);
        Assert.False(string.IsNullOrWhiteSpace(finalAuth.Token));
        Assert.False(string.IsNullOrWhiteSpace(finalAuth.RefreshToken));
    }
}
