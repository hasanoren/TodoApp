using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TodoApp.Application.DTOs;
using TodoApp.Domain.Entities;
using Xunit;

namespace TodoApp.IntegrationTests;

public class TodoItemFilterIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TodoItemFilterIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(HttpClient Client, string Email)> CreateAuthenticatedClientAsync()
    {
        var email = $"filter_user_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest
        {
            Email = email,
            Password = "Password123!"
        };

        var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        var client = new HttpClient(_client.BaseAddress != null ? new HttpClientHandler() : null!)
        {
            BaseAddress = _client.BaseAddress
        };
        // Reuse default factory request handler
        return (client, email);
    }

    private async Task<string> AuthenticateUserAsync(string? email = null)
    {
        var userEmail = email ?? $"filter_user_{Guid.NewGuid():N}@example.com";
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
    public async Task GetAll_WithSearchTerm_FiltersByTitleAndDescription()
    {
        // ARRANGE
        var token = await AuthenticateUserAsync();
        using var requestMessage = new HttpRequestMessage();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var taskA = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "Sprint Planlama Toplantısı",
            Description = "Haftalık sprint maddelerinin gözden geçirilmesi",
            DueDate = DateTime.UtcNow.AddDays(2)
        });
        Assert.Equal(HttpStatusCode.Created, taskA.StatusCode);

        var taskB = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "Market Alışverişi",
            Description = "Ev ihtiyaçları listesi",
            DueDate = DateTime.UtcNow.AddDays(3)
        });
        Assert.Equal(HttpStatusCode.Created, taskB.StatusCode);

        // ACT - Search by title keyword "Sprint"
        var searchTitleResp = await _client.GetAsync("/api/TodoItems?search=sprint");
        Assert.Equal(HttpStatusCode.OK, searchTitleResp.StatusCode);
        var resultTitle = await searchTitleResp.Content.ReadFromJsonAsync<PaginatedResponse<TodoItemResponse>>(JsonOptions);

        // ACT - Search by description keyword "ihtiyaçları"
        var searchDescResp = await _client.GetAsync("/api/TodoItems?search=ihtiya%C3%A7lar%C4%B1");
        Assert.Equal(HttpStatusCode.OK, searchDescResp.StatusCode);
        var resultDesc = await searchDescResp.Content.ReadFromJsonAsync<PaginatedResponse<TodoItemResponse>>(JsonOptions);

        // ASSERT
        Assert.NotNull(resultTitle);
        Assert.Single(resultTitle.Items);
        Assert.Equal("Sprint Planlama Toplantısı", resultTitle.Items[0].Title);

        Assert.NotNull(resultDesc);
        Assert.Single(resultDesc.Items);
        Assert.Equal("Market Alışverişi", resultDesc.Items[0].Title);
    }

    [Fact]
    public async Task GetAll_WithStatusFilter_ReturnsOnlyMatchingStatus()
    {
        // ARRANGE
        var token = await AuthenticateUserAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createOpen = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "Açık Görev",
            Description = "Henüz tamamlanmadı",
            DueDate = DateTime.UtcNow.AddDays(1)
        });
        var openItem = await createOpen.Content.ReadFromJsonAsync<TodoItemResponse>(JsonOptions);

        var createCompleted = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "Tamamlanmış Görev",
            Description = "Tamamlandı",
            DueDate = DateTime.UtcNow.AddDays(1)
        });
        var completedItem = await createCompleted.Content.ReadFromJsonAsync<TodoItemResponse>(JsonOptions);
        await _client.PatchAsync($"/api/TodoItems/{completedItem!.Id}/complete", null);

        // ACT - Query Status=Completed
        var respCompleted = await _client.GetAsync("/api/TodoItems?status=Completed");
        var resultCompleted = await respCompleted.Content.ReadFromJsonAsync<PaginatedResponse<TodoItemResponse>>(JsonOptions);

        // ACT - Query Status=Open
        var respOpen = await _client.GetAsync("/api/TodoItems?status=Open");
        var resultOpen = await respOpen.Content.ReadFromJsonAsync<PaginatedResponse<TodoItemResponse>>(JsonOptions);

        // ASSERT
        Assert.NotNull(resultCompleted);
        Assert.Contains(resultCompleted.Items, t => t.Id == completedItem.Id);
        Assert.DoesNotContain(resultCompleted.Items, t => t.Id == openItem!.Id);

        Assert.NotNull(resultOpen);
        Assert.Contains(resultOpen.Items, t => t.Id == openItem!.Id);
        Assert.DoesNotContain(resultOpen.Items, t => t.Id == completedItem.Id);
    }

    [Fact]
    public async Task GetAll_WithFilterType_FiltersOnlyMineAndSharedTasks()
    {
        // ARRANGE: User 1 and User 2
        var user1Email = $"user1_{Guid.NewGuid():N}@example.com";
        var user2Email = $"user2_{Guid.NewGuid():N}@example.com";

        var tokenUser1 = await AuthenticateUserAsync(user1Email);
        var tokenUser2 = await AuthenticateUserAsync(user2Email);

        // User 1 creates task 1 (unshared)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenUser1);
        var createResp1 = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "User1 Yalnızca Kendine Ait Görev",
            DueDate = DateTime.UtcNow.AddDays(1)
        });
        var task1 = await createResp1.Content.ReadFromJsonAsync<TodoItemResponse>(JsonOptions);

        // User 1 creates task 2 and shares it with User 2
        var createResp2 = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "User1 Paylaşılan Görev",
            DueDate = DateTime.UtcNow.AddDays(2)
        });
        var task2 = await createResp2.Content.ReadFromJsonAsync<TodoItemResponse>(JsonOptions);
        await _client.PostAsJsonAsync($"/api/TodoItems/{task2!.Id}/shares", new ShareTaskRequest { Email = user2Email });

        // ACT: User 1 queries OnlyMine vs SharedByMe
        var onlyMineResp = await _client.GetAsync("/api/TodoItems?filterType=OnlyMine");
        var onlyMineResult = await onlyMineResp.Content.ReadFromJsonAsync<PaginatedResponse<TodoItemResponse>>(JsonOptions);

        var sharedByMeResp = await _client.GetAsync("/api/TodoItems?filterType=SharedByMe");
        var sharedByMeResult = await sharedByMeResp.Content.ReadFromJsonAsync<PaginatedResponse<TodoItemResponse>>(JsonOptions);

        // ACT: User 2 queries SharedWithMe
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenUser2);
        var sharedWithMeResp = await _client.GetAsync("/api/TodoItems?filterType=SharedWithMe");
        var sharedWithMeResult = await sharedWithMeResp.Content.ReadFromJsonAsync<PaginatedResponse<TodoItemResponse>>(JsonOptions);

        // ASSERT
        Assert.NotNull(onlyMineResult);
        Assert.Contains(onlyMineResult.Items, t => t.Id == task1!.Id);
        Assert.Contains(onlyMineResult.Items, t => t.Id == task2.Id);

        Assert.NotNull(sharedByMeResult);
        Assert.Contains(sharedByMeResult.Items, t => t.Id == task2.Id);
        Assert.DoesNotContain(sharedByMeResult.Items, t => t.Id == task1!.Id);

        Assert.NotNull(sharedWithMeResult);
        Assert.Contains(sharedWithMeResult.Items, t => t.Id == task2.Id);
    }

    [Fact]
    public async Task GetAll_WithDueDateRangeAndSorting_FiltersAndSortsCorrectly()
    {
        // ARRANGE
        var token = await AuthenticateUserAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var now = DateTime.UtcNow.Date;

        var taskEarly = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "Erken Görev",
            DueDate = now.AddDays(2)
        });
        var itemEarly = await taskEarly.Content.ReadFromJsonAsync<TodoItemResponse>(JsonOptions);

        var taskLate = await _client.PostAsJsonAsync("/api/TodoItems", new CreateTodoItemRequest
        {
            Title = "Geç Görev",
            DueDate = now.AddDays(10)
        });
        var itemLate = await taskLate.Content.ReadFromJsonAsync<TodoItemResponse>(JsonOptions);

        // ACT: Filter by DueDateFrom = now.AddDays(5) -> should only return itemLate
        var dateFromQuery = Uri.EscapeDataString(now.AddDays(5).ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var filterResp = await _client.GetAsync($"/api/TodoItems?dueDateFrom={dateFromQuery}");
        var filterResult = await filterResp.Content.ReadFromJsonAsync<PaginatedResponse<TodoItemResponse>>(JsonOptions);

        // ACT: Sort by DueDate Ascending -> itemEarly first, itemLate second
        var sortResp = await _client.GetAsync("/api/TodoItems?sortBy=dueDate&sortOrder=asc");
        var sortResult = await sortResp.Content.ReadFromJsonAsync<PaginatedResponse<TodoItemResponse>>(JsonOptions);

        // ASSERT
        Assert.NotNull(filterResult);
        Assert.Contains(filterResult.Items, t => t.Id == itemLate!.Id);
        Assert.DoesNotContain(filterResult.Items, t => t.Id == itemEarly!.Id);

        Assert.NotNull(sortResult);
        var earlyIndex = sortResult.Items.FindIndex(t => t.Id == itemEarly!.Id);
        var lateIndex = sortResult.Items.FindIndex(t => t.Id == itemLate!.Id);
        Assert.True(earlyIndex < lateIndex, "Erken görev, geç görevden önce listelenmeli (dueDate asc).");
    }
}
