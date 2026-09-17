using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TodoApp.Application.DTOs;
using TodoApp.Domain.Entities;
using Xunit;

namespace TodoApp.IntegrationTests;

public class TodoListIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TodoListIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> AuthenticateUserAsync(string? email = null)
    {
        var userEmail = email ?? $"list_user_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest
        {
            Email = userEmail,
            Password = "Password123!"
        };

        var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return result!.Token;
    }

    [Fact]
    public async Task CreateList_AssignTaskToList_FilterByList_WorksCorrectly()
    {
        // ARRANGE
        var token = await AuthenticateUserAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Create a list
        var listResponse = await _client.PostAsJsonAsync("/api/TodoLists", new CreateTodoListRequest
        {
            Name = "Work Projects",
            ColorCode = "#FF0000"
        });

        Assert.Equal(HttpStatusCode.Created, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<TodoListResponse>(JsonOptions);
        Assert.NotNull(list);

        // 2. Create tasks (one in the list, one outside)
        var taskInList = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "Do work stuff",
            TodoListId = list.Id
        });
        var itemInList = await taskInList.Content.ReadFromJsonAsync<TodoItemResponse>(JsonOptions);

        var taskOutside = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "Do home stuff"
        });
        var itemOutside = await taskOutside.Content.ReadFromJsonAsync<TodoItemResponse>(JsonOptions);

        // 3. Filter by list id
        var filterResp = await _client.GetAsync($"/api/TodoItems?todoListId={list.Id}");
        var filterResult = await filterResp.Content.ReadFromJsonAsync<PaginatedResponse<TodoItemResponse>>(JsonOptions);

        // ASSERT
        Assert.NotNull(filterResult);
        Assert.Single(filterResult.Items);
        Assert.Equal(itemInList!.Id, filterResult.Items[0].Id);
    }
}

