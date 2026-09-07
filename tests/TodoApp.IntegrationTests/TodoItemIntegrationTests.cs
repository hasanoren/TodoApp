using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TodoApp.Application.DTOs;
using Xunit;

namespace TodoApp.IntegrationTests;

public class TodoItemIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TodoItemIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> AuthenticateUserAsync(string? email = null)
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

        return result!.Token;
    }

    [Fact]
    public async Task TodoItem_CompleteLifecycle_Succeeds()
    {
        // ARRANGE
        var token = await AuthenticateUserAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. CREATE TASK
        var createRequest = new CreateTodoItemRequest
        {
            Title = "API Entegrasyon Test Görevi",
            Description = "Entegrasyon testi için oluşturuldu.",
            DueDate = DateTime.UtcNow.AddDays(3)
        };

        var createResponse = await _client.PostAsJsonAsync("/api/TodoItems", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdTask = await createResponse.Content.ReadFromJsonAsync<TodoItemResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(createdTask);
        Assert.Equal("API Entegrasyon Test Görevi", createdTask.Title);
        Assert.Equal("Open", createdTask.Status);

        // 2. GET BY ID
        var getResponse = await _client.GetAsync($"/api/TodoItems/{createdTask.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        // 3. UPDATE TASK
        var updateRequest = new UpdateTodoItemRequest
        {
            Title = "Güncellenmiş Test Görevi",
            Description = "Açıklama güncellendi",
            DueDate = DateTime.UtcNow.AddDays(5)
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/TodoItems/{createdTask.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // 4. COMPLETE TASK (PATCH)
        var completeResponse = await _client.PatchAsync($"/api/TodoItems/{createdTask.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var completedTask = await completeResponse.Content.ReadFromJsonAsync<TodoItemResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("Completed", completedTask!.Status);

        // 5. GET ALL PAGINATED
        var listResponse = await _client.GetAsync("/api/TodoItems?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        // 6. SOFT DELETE (DELETE)
        var deleteResponse = await _client.DeleteAsync($"/api/TodoItems/{createdTask.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Aktif listede artık görünmemeli (404)
        var getAfterDelete = await _client.GetAsync($"/api/TodoItems/{createdTask.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);

        // 7. GET TRASH
        var trashResponse = await _client.GetAsync("/api/TodoItems/trash?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, trashResponse.StatusCode);

        // 8. RESTORE
        var restoreResponse = await _client.PostAsync($"/api/TodoItems/{createdTask.Id}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restoreResponse.StatusCode);

        // Tekrar aktif listede görünmeli (200)
        var getAfterRestore = await _client.GetAsync($"/api/TodoItems/{createdTask.Id}");
        Assert.Equal(HttpStatusCode.OK, getAfterRestore.StatusCode);

        // 9. RE-DELETE & PERMANENT DELETE
        await _client.DeleteAsync($"/api/TodoItems/{createdTask.Id}");
        var permanentDeleteResponse = await _client.DeleteAsync($"/api/TodoItems/{createdTask.Id}/permanent");
        Assert.Equal(HttpStatusCode.NoContent, permanentDeleteResponse.StatusCode);

        // Çöpte de kalmamalı
        var getAfterPermanent = await _client.GetAsync($"/api/TodoItems/{createdTask.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterPermanent.StatusCode);
    }
}
