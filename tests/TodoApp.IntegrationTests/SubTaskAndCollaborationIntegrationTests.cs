using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TodoApp.Application.DTOs;
using Xunit;

namespace TodoApp.IntegrationTests;

public class SubTaskAndCollaborationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SubTaskAndCollaborationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, string Email)> CreateUserAsync()
    {
        var email = $"user_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest
        {
            Email = email,
            Password = "Password123!"
        };

        var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return (result!.Token, email);
    }

    [Fact]
    public async Task SubTask_Lifecycle_And_OwnerOnlyDelete_Enforced()
    {
        // ARRANGE: Owner ve SharedUser oluştur
        var (ownerToken, _) = await CreateUserAsync();
        var (sharedToken, sharedEmail) = await CreateUserAsync();

        // 1. Owner bir görev oluşturur
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var taskResponse = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "İş Birliği Görevi",
            Description = "Alt görevler ve paylaşım testi"
        });
        var task = await taskResponse.Content.ReadFromJsonAsync<TodoItemResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // 2. Görevi ikinci kullanıcıyla paylaş
        var shareResponse = await _client.PostAsJsonAsync($"/api/todoitems/{task!.Id}/shares", new ShareTaskRequest
        {
            Email = sharedEmail
        });
        Assert.Equal(HttpStatusCode.OK, shareResponse.StatusCode);

        // 3. SharedUser alt görev ekler (BR-020: İzinli)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sharedToken);

        var createSubTaskResponse = await _client.PostAsJsonAsync($"/api/todoitems/{task.Id}/subtasks", new CreateSubTaskRequest
        {
            Title = "Paylaşılan kullanıcının eklediği alt görev"
        });
        Assert.Equal(HttpStatusCode.Created, createSubTaskResponse.StatusCode);

        var subTask = await createSubTaskResponse.Content.ReadFromJsonAsync<SubTaskResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // 4. SharedUser alt görevi tamamlar (BR-020: İzinli)
        var completeSubTaskResponse = await _client.PatchAsync($"/api/subtasks/{subTask!.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeSubTaskResponse.StatusCode);

        // 5. KRİTİK KURAL (BR-020, BR-026): SharedUser alt görevi SİLEMEZ (404 almalı)
        var sharedDeleteResponse = await _client.DeleteAsync($"/api/subtasks/{subTask.Id}");
        Assert.Equal(HttpStatusCode.NotFound, sharedDeleteResponse.StatusCode);

        // 6. Owner alt görevi silebilir (204)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var ownerDeleteResponse = await _client.DeleteAsync($"/api/subtasks/{subTask.Id}");
        Assert.Equal(HttpStatusCode.NoContent, ownerDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task OwnershipTransfer_Flow_Succeeds()
    {
        // ARRANGE
        var (ownerToken, _) = await CreateUserAsync();
        var (targetToken, targetEmail) = await CreateUserAsync();

        // 1. Owner görev oluşturur
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var taskResponse = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "Devredilecek Görev",
            Description = "Sahiplik devri testi"
        });
        var task = await taskResponse.Content.ReadFromJsonAsync<TodoItemResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // 2. Devir talebi başlat (BR-030)
        var transferRequestResponse = await _client.PostAsJsonAsync(
            $"/api/todoitems/{task!.Id}/transfer-requests",
            new CreateTransferRequestDto { NewOwnerEmail = targetEmail });
        Assert.Equal(HttpStatusCode.OK, transferRequestResponse.StatusCode);

        var transferResult = await transferRequestResponse.Content.ReadFromJsonAsync<TransferRequestResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // 3. Hedef kullanıcı bekleyen talepleri listeler
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", targetToken);

        var pendingResponse = await _client.GetAsync("/api/transfer-requests/pending");
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);

        var pendingList = await pendingResponse.Content.ReadFromJsonAsync<List<TransferRequestResponse>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Contains(pendingList!, r => r.Id == transferResult!.Id);

        // 4. Hedef kullanıcı talebi kabul eder
        var acceptResponse = await _client.PostAsync($"/api/transfer-requests/{transferResult!.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        // 5. Yeni sahip görevi silmeyi dener -> Artık sahibi olduğu için başarılı olmalı (204)
        var newOwnerDeleteResponse = await _client.DeleteAsync($"/api/TodoItems/{task.Id}");
        Assert.Equal(HttpStatusCode.NoContent, newOwnerDeleteResponse.StatusCode);
    }
}
