using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class TodoItemService : ITodoItemService
{
    private readonly ITodoItemRepository _todoItemRepository;
    private readonly ITaskAuthorizationService _taskAuthorizationService;

    public TodoItemService(
        ITodoItemRepository todoItemRepository,
        ITaskAuthorizationService taskAuthorizationService)
    {
        _todoItemRepository = todoItemRepository;
        _taskAuthorizationService = taskAuthorizationService;
    }

    public async Task<TodoItemResponse> CreateAsync(Guid userId, CreateTodoItemRequest request)
    {
        var todoItem = new TodoItem
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,           // BR-006: owner NOT NULL
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate,
            Status = TodoItemStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        await _todoItemRepository.AddAsync(todoItem);
        await _todoItemRepository.SaveChangesAsync();

        return MapToResponse(todoItem, userId);
    }

    public async Task<TodoItemResponse> GetByIdAsync(Guid userId, Guid todoItemId)
    {
        var todoItem = await _taskAuthorizationService.EnsureCanReadAsync(todoItemId, userId);
        return MapToResponse(todoItem, userId);
    }

    public async Task<PaginatedResponse<TodoItemResponse>> GetAllAsync(Guid userId, PaginatedRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, PaginatedRequest.MaxPageSize);

        // BR-011: IsDeleted=false filtresi repository'de uygulanıyor
        var (items, totalCount) = await _todoItemRepository.GetAccessibleByUserAsync(userId, page, pageSize);
        var mappedItems = items.Select(item => MapToResponse(item, userId)).ToList();

        return new PaginatedResponse<TodoItemResponse>(mappedItems, totalCount, page, pageSize);
    }

    public async Task<TodoItemResponse> UpdateAsync(
        Guid userId, Guid todoItemId, UpdateTodoItemRequest request)
    {
        var todoItem = await _taskAuthorizationService.EnsureCanModifyAsync(todoItemId, userId);

        todoItem.Title = request.Title;
        todoItem.Description = request.Description;
        todoItem.DueDate = request.DueDate;

        await _todoItemRepository.SaveChangesAsync();

        return MapToResponse(todoItem, userId);
    }

    public async Task<TodoItemResponse> CompleteAsync(Guid userId, Guid todoItemId)
    {
        var todoItem = await _taskAuthorizationService.EnsureCanCompleteAsync(todoItemId, userId);

        // BR-015: CompletedByUserId ve CompletedAt set edilir, paylaşım kalksa da korunur
        todoItem.Status = TodoItemStatus.Completed;
        todoItem.CompletedByUserId = userId;
        todoItem.CompletedAt = DateTime.UtcNow;

        await _todoItemRepository.SaveChangesAsync();

        return MapToResponse(todoItem, userId);
    }

    public async Task DeleteAsync(Guid userId, Guid todoItemId)
    {
        // BR-008 & BR-026: Yalnızca görev sahibi silebilir! Paylaşılan kullanıcılar silemez
        var todoItem = await _taskAuthorizationService.EnsureCanDeleteAsync(todoItemId, userId);

        // BR-008: Soft delete uygulanır (çöp kutusuna gider, restore edilebilir)
        todoItem.IsDeleted = true;
        todoItem.DeletedByUserId = userId;
        todoItem.DeletedAt = DateTime.UtcNow;

        await _todoItemRepository.SaveChangesAsync();
    }

    public async Task PermanentDeleteAsync(Guid userId, Guid todoItemId)
    {
        // BR-010, BR-029: Sadece owner kalıcı silebilir
        var todoItem = await _taskAuthorizationService.EnsureOwnerAsync(todoItemId, userId);

        if (!todoItem.IsDeleted)
        {
            throw new ValidationException("Yalnızca çöp kutusundaki görevler kalıcı olarak silinebilir.");
        }

        _todoItemRepository.Delete(todoItem);
        await _todoItemRepository.SaveChangesAsync();
    }

    public async Task<TodoItemResponse> RestoreAsync(Guid userId, Guid todoItemId)
    {
        // BR-010: Sadece owner restore edebilir
        var todoItem = await _taskAuthorizationService.EnsureOwnerAsync(todoItemId, userId);

        if (!todoItem.IsDeleted)
        {
            throw new ValidationException("Bu görev zaten aktif durumda.");
        }

        todoItem.IsDeleted = false;
        todoItem.DeletedByUserId = null;
        todoItem.DeletedAt = null;

        await _todoItemRepository.SaveChangesAsync();

        return MapToResponse(todoItem, userId);
    }

    public async Task<PaginatedResponse<TodoItemResponse>> GetTrashAsync(Guid userId, PaginatedRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, PaginatedRequest.MaxPageSize);

        var (items, totalCount) = await _todoItemRepository.GetDeletedByOwnerAsync(userId, page, pageSize);
        var mappedItems = items.Select(item => MapToResponse(item, userId)).ToList();

        return new PaginatedResponse<TodoItemResponse>(mappedItems, totalCount, page, pageSize);
    }

    private static TodoItemResponse MapToResponse(TodoItem todoItem, Guid currentUserId)
    {
        return new TodoItemResponse
        {
            Id = todoItem.Id,
            Title = todoItem.Title,
            Description = todoItem.Description,
            DueDate = todoItem.DueDate,
            Status = todoItem.Status.ToString(),
            OwnerId = todoItem.OwnerId,
            IsOwner = todoItem.OwnerId == currentUserId,
            CompletedByUserId = todoItem.CompletedByUserId,
            CompletedAt = todoItem.CompletedAt,
            CreatedAt = todoItem.CreatedAt,
            SubTasks = todoItem.SubTasks?.Select(st => new SubTaskResponse
            {
                Id = st.Id,
                TaskId = st.TaskId,
                Title = st.Title,
                Status = st.Status.ToString(),
                CreatedAt = st.CreatedAt
            }).ToList() ?? new List<SubTaskResponse>(),
            Tags = todoItem.TodoItemTags?.Select(tit => new TagResponse
            {
                Id = tit.Tag?.Id ?? tit.TagId,
                Name = tit.Tag?.Name ?? string.Empty,
                CreatedAt = tit.Tag?.CreatedAt ?? tit.AssignedAt
            }).ToList() ?? new List<TagResponse>(),
            SharedWith = todoItem.TaskShares?.Select(ts => new SharedUserResponse
            {
                UserId = ts.UserId,
                Email = ts.User?.Email ?? string.Empty,
                SharedAt = ts.SharedAt
            }).ToList() ?? new List<SharedUserResponse>()
        };
    }
}
