using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Application.DTOs;
using Xunit;

namespace TodoApp.IntegrationTests;

public class RateLimitingIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RateLimitingIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ExceedingLimit_Returns429TooManyRequests()
    {
        // ARRANGE: Limiti 3 olan register endpoint'i için aynı IP'den 4 istek gönder
        var client = _factory.CreateClient();
        var clientIp = $"10.0.1.{Random.Shared.Next(10, 250)}";
        client.DefaultRequestHeaders.Add("X-Forwarded-For", clientIp);

        var request = new RegisterRequest
        {
            Email = $"ratelimit_reg_{Guid.NewGuid():N}@example.com",
            Password = "Password123!"
        };

        // ACT: İlk 3 istek izin verilen limit dahilinde olmalı
        for (int i = 0; i < 3; i++)
        {
            var allowedResponse = await client.PostAsJsonAsync("/api/Auth/register", request);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowedResponse.StatusCode);
        }

        // 4. istek limiti (3/dk) aştığı için 429 dönmeli
        var throttledResponse = await client.PostAsJsonAsync("/api/Auth/register", request);

        // ASSERT
        Assert.Equal(HttpStatusCode.TooManyRequests, throttledResponse.StatusCode);
        Assert.Equal("application/problem+json", throttledResponse.Content.Headers.ContentType?.MediaType);

        var problem = await throttledResponse.Content.ReadFromJsonAsync<ProblemDetails>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(problem);
        Assert.Equal(429, problem.Status);
        Assert.Equal("/api/Auth/register", problem.Instance);
    }

    [Fact]
    public async Task Login_ExceedingLimit_Returns429TooManyRequests()
    {
        // ARRANGE: Limiti 5 olan login endpoint'i için aynı IP'den 6 istek gönder
        var client = _factory.CreateClient();
        var clientIp = $"10.0.2.{Random.Shared.Next(10, 250)}";
        client.DefaultRequestHeaders.Add("X-Forwarded-For", clientIp);

        var request = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "WrongPassword123!"
        };

        // ACT: İlk 5 istek izin verilen limit dahilinde olmalı
        for (int i = 0; i < 5; i++)
        {
            var allowedResponse = await client.PostAsJsonAsync("/api/Auth/login", request);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowedResponse.StatusCode);
        }

        // 6. istek limiti (5/dk) aştığı için 429 dönmeli
        var throttledResponse = await client.PostAsJsonAsync("/api/Auth/login", request);

        // ASSERT
        Assert.Equal(HttpStatusCode.TooManyRequests, throttledResponse.StatusCode);
        Assert.Equal("application/problem+json", throttledResponse.Content.Headers.ContentType?.MediaType);

        var problem = await throttledResponse.Content.ReadFromJsonAsync<ProblemDetails>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(problem);
        Assert.Equal(429, problem.Status);
        Assert.Equal("/api/Auth/login", problem.Instance);
    }

    [Fact]
    public async Task ForgotPassword_ExceedingLimit_Returns429TooManyRequests()
    {
        // ARRANGE: Limiti 2 olan forgot-password endpoint'i için aynı IP'den 3 istek gönder
        var client = _factory.CreateClient();
        var clientIp = $"10.0.3.{Random.Shared.Next(10, 250)}";
        client.DefaultRequestHeaders.Add("X-Forwarded-For", clientIp);

        var request = new ForgotPasswordRequest
        {
            Email = "user@example.com"
        };

        // ACT: İlk 2 istek izin verilen limit dahilinde olmalı
        for (int i = 0; i < 2; i++)
        {
            var allowedResponse = await client.PostAsJsonAsync("/api/Auth/forgot-password", request);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowedResponse.StatusCode);
        }

        // 3. istek limiti (2/dk) aştığı için 429 dönmeli
        var throttledResponse = await client.PostAsJsonAsync("/api/Auth/forgot-password", request);

        // ASSERT
        Assert.Equal(HttpStatusCode.TooManyRequests, throttledResponse.StatusCode);
        Assert.Equal("application/problem+json", throttledResponse.Content.Headers.ContentType?.MediaType);

        var problem = await throttledResponse.Content.ReadFromJsonAsync<ProblemDetails>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(problem);
        Assert.Equal(429, problem.Status);
        Assert.Equal("/api/Auth/forgot-password", problem.Instance);
    }
}

