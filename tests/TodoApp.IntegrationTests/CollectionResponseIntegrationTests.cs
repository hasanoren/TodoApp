using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TodoApp.Application.DTOs;
using TodoApp.Domain.Entities;
using TodoApp.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TodoApp.IntegrationTests;

public class CollectionResponseIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public CollectionResponseIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Token, Guid UserId)> AuthenticateUserAsync(string? email = null, UserRole role = UserRole.User)
    {
        var userEmail = email ?? $"user_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest
        {
            Email = userEmail,
            Password = "Password123!"
        };

        var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (role == UserRole.Admin)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.FindAsync(result!.UserId);
            if (user != null)
            {
                user.Role = UserRole.Admin;
                await db.SaveChangesAsync();
            }

            // Yeniden login ol ki Admin rolü JWT claim'ine yansısın
            var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", new LoginRequest
            {
                Email = userEmail,
                Password = "Password123!"
            });
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return (loginResult!.Token, loginResult.UserId);
        }

        return (result!.Token, result.UserId);
    }

    [Fact]
    public async Task GetSubTasks_ReturnsWrappedCollectionResponse()
    {
        var (token, _) = await AuthenticateUserAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Task oluştur
        var taskRes = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "Ana Görev - Alt Görev Testi"
        });
        var task = await taskRes.Content.ReadFromJsonAsync<TodoItemResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // 2. SubTask ekle
        await _client.PostAsJsonAsync($"/api/todoitems/{task!.Id}/subtasks", new CreateSubTaskRequest
        {
            Title = "Alt Görev 1"
        });

        // 3. GET /api/todoitems/{taskId}/subtasks çağır
        var response = await _client.GetAsync($"/api/todoitems/{task.Id}/subtasks");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        // JSON kök seviyesinde "items" anahtarı içermeli, düz dizi ile başlamamalı
        Assert.StartsWith("{", rawJson.Trim());
        Assert.Contains("\"items\"", rawJson);

        var result = await response.Content.ReadFromJsonAsync<CollectionResponse<SubTaskResponse>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.NotEmpty(result.Items);
        Assert.Equal("Alt Görev 1", result.Items[0].Title);
    }

    [Fact]
    public async Task GetTags_ReturnsWrappedCollectionResponse()
    {
        var (adminToken, _) = await AuthenticateUserAsync(role: UserRole.Admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 1. Tag oluştur
        var tagUniqueName = $"tag_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/tags", new CreateTagRequest { Name = tagUniqueName });

        // 2. GET /api/tags çağır
        var response = await _client.GetAsync("/api/tags");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("{", rawJson.Trim());
        Assert.Contains("\"items\"", rawJson);

        var result = await response.Content.ReadFromJsonAsync<CollectionResponse<TagResponse>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.Contains(result.Items, t => t.Name == tagUniqueName);
    }

    [Fact]
    public async Task GetShares_ReturnsWrappedCollectionResponse()
    {
        var (ownerToken, _) = await AuthenticateUserAsync();
        var (targetToken, _) = await AuthenticateUserAsync();
        var targetEmail = $"target_{Guid.NewGuid():N}@example.com";
        await AuthenticateUserAsync(targetEmail);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // 1. Task oluştur
        var taskRes = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "Paylaşım Test Görevi"
        });
        var task = await taskRes.Content.ReadFromJsonAsync<TodoItemResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // 2. Paylaş
        await _client.PostAsJsonAsync($"/api/todoitems/{task!.Id}/shares", new ShareTaskRequest
        {
            Email = targetEmail
        });

        // 3. GET /api/todoitems/{taskId}/shares çağır
        var response = await _client.GetAsync($"/api/todoitems/{task.Id}/shares");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("{", rawJson.Trim());
        Assert.Contains("\"items\"", rawJson);

        var result = await response.Content.ReadFromJsonAsync<CollectionResponse<SharedUserResponse>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, u => u.Email == targetEmail);
    }

    [Fact]
    public async Task GetActivities_ReturnsWrappedCollectionResponse()
    {
        var (token, _) = await AuthenticateUserAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Task oluştur
        var taskRes = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "Aktivite Testi Görevi"
        });
        var task = await taskRes.Content.ReadFromJsonAsync<TodoItemResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // 2. GET /api/todoitems/{taskId}/activities çağır
        var response = await _client.GetAsync($"/api/todoitems/{task!.Id}/activities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("{", rawJson.Trim());
        Assert.Contains("\"items\"", rawJson);

        var result = await response.Content.ReadFromJsonAsync<CollectionResponse<TodoItemActivityResponse>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.NotNull(result.Items);
    }
}

