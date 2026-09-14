using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Application.DTOs;
using Xunit;

namespace TodoApp.IntegrationTests;

public class ProblemDetailsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ProblemDetailsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ValidationError_Returns_Rfc7807_ValidationProblemDetails()
    {
        // ARRANGE: Invalid request (invalid email, short password)
        var invalidRequest = new RegisterRequest
        {
            Email = "not-an-email",
            Password = ""
        };

        // ACT
        var response = await _client.PostAsJsonAsync("/api/Auth/register", invalidRequest);

        // ASSERT
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType.ToString());

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Equal("Doğrulama Hatası", problem.Title);
        Assert.Equal("/api/Auth/register", problem.Instance);
        Assert.Contains("https://tools.ietf.org/html/rfc9110#section-15.5.1", problem.Type);
        Assert.NotEmpty(problem.Errors);
    }

    [Fact]
    public async Task ConflictException_Returns_Rfc7807_ProblemDetails()
    {
        // ARRANGE: Register user once
        var email = $"conflict_{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest { Email = email, Password = "Password123!" };
        var firstResponse = await _client.PostAsJsonAsync("/api/Auth/register", request);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // ACT: Register same email again to trigger ConflictException
        var secondResponse = await _client.PostAsJsonAsync("/api/Auth/register", request);

        // ASSERT
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.NotNull(secondResponse.Content.Headers.ContentType);
        Assert.Contains("application/problem+json", secondResponse.Content.Headers.ContentType.ToString());

        var problem = await secondResponse.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Equal("Çakışma Hatası", problem.Title);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.10", problem.Type);
        Assert.Equal("/api/Auth/register", problem.Instance);
        Assert.Equal("Bu e-posta adresi zaten kayıtlı.", problem.Detail);
    }

    [Fact]
    public async Task NotFoundException_Returns_Rfc7807_ProblemDetails()
    {
        // ARRANGE: Authenticate user
        var email = $"notfound_test_{Guid.NewGuid():N}@example.com";
        var registerResponse = await _client.PostAsJsonAsync("/api/Auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123!"
        });
        var authResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(authResult);

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/todoitems/{Guid.NewGuid()}");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.Token);

        // ACT
        var response = await _client.SendAsync(requestMessage);

        // ASSERT
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType.ToString());

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Equal("Kayıt Bulunamadı", problem.Title);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.5", problem.Type);
        Assert.Contains("/api/todoitems/", problem.Instance);
    }
}

