using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class TodoItemService : ITodoItemService
{
    private readonly ITodoItemRepository _todoItemRepository;

    public TodoItemService(ITodoItemRepository todoItemRepository)
    {
        _todoItemRepository = todoItemRepository;
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
        var todoItem = await GetAuthorizedTodoItemAsync(userId, todoItemId);
        return MapToResponse(todoItem, userId);
    }

    public async Task<List<TodoItemResponse>> GetAllAsync(Guid userId)
    {
        // BR-011: IsDeleted=false filtresi repository'de uygulanıyor
        var items = await _todoItemRepository.GetAccessibleByUserAsync(userId);
        return items.Select(item => MapToResponse(item, userId)).ToList();
    }

    public async Task<TodoItemResponse> UpdateAsync(
        Guid userId, Guid todoItemId, UpdateTodoItemRequest request)
    {
        var todoItem = await GetAuthorizedTodoItemAsync(userId, todoItemId);

        todoItem.Title = request.Title;
        todoItem.Description = request.Description;
        todoItem.DueDate = request.DueDate;

        await _todoItemRepository.SaveChangesAsync();

        return MapToResponse(todoItem, userId);
    }

    public async Task<TodoItemResponse> CompleteAsync(Guid userId, Guid todoItemId)
    {
        var todoItem = await GetAuthorizedTodoItemAsync(userId, todoItemId);

        // BR-015: CompletedByUserId ve CompletedAt set edilir, paylaşım kalksa da korunur
        todoItem.Status = TodoItemStatus.Completed;
        todoItem.CompletedByUserId = userId;
        todoItem.CompletedAt = DateTime.UtcNow;

        await _todoItemRepository.SaveChangesAsync();

        return MapToResponse(todoItem, userId);
    }

    public async Task DeleteAsync(Guid userId, Guid todoItemId)
    {
        var todoItem = await GetAuthorizedTodoItemAsync(userId, todoItemId);

        // BR-008 & BR-026: Yalnızca görev sahibi silebilir! Paylaşılan kullanıcılar silemez
        if (todoItem.OwnerId != userId)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        // BR-008: Soft delete uygulanır (çöp kutusuna gider, restore edilebilir)
        todoItem.IsDeleted = true;
        todoItem.DeletedByUserId = userId;
        todoItem.DeletedAt = DateTime.UtcNow;

        await _todoItemRepository.SaveChangesAsync();
    }

    public async Task<TodoItemResponse> RestoreAsync(Guid userId, Guid todoItemId)
    {
        var todoItem = await _todoItemRepository.GetByIdAsync(todoItemId);

        if (todoItem is null || todoItem.OwnerId != userId)
        {
            // BR-029: yetkisiz erişimde 404
            throw new NotFoundException("Görev bulunamadı.");
        }

        if (!todoItem.IsDeleted)
        {
            throw new ValidationException("Bu görev zaten aktif durumda.");
        }

        // BR-010: Sadece owner restore edebilir (yukarıda kontrol edildi)
        todoItem.IsDeleted = false;
        todoItem.DeletedByUserId = null;
        todoItem.DeletedAt = null;

        await _todoItemRepository.SaveChangesAsync();

        return MapToResponse(todoItem, userId);
    }

    public async Task<List<TodoItemResponse>> GetTrashAsync(Guid userId)
    {
        var items = await _todoItemRepository.GetDeletedByOwnerAsync(userId);
        return items.Select(item => MapToResponse(item, userId)).ToList();
    }

    // --- Yardımcı Metotlar ---

    /// <summary>
    /// Görev ID'sine göre görev getirir ve kullanıcının yetkisini kontrol eder.
    /// BR-025 & BR-029: Owner veya TaskShare'deki kullanıcılar erişebilir.
    /// </summary>
    private async Task<TodoItem> GetAuthorizedTodoItemAsync(Guid userId, Guid todoItemId)
    {
        var todoItem = await _todoItemRepository.GetByIdAsync(todoItemId);

        if (todoItem is null)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        var isOwner = todoItem.OwnerId == userId;
        var isShared = todoItem.TaskShares != null && todoItem.TaskShares.Any(ts => ts.UserId == userId);

        // BR-029: Owner veya TaskShare'de kayıtlı olmayan kullanıcı → 404
        if (!isOwner && !isShared)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        // BR-011: Soft-delete edilmiş görev aktif listede görünmez
        if (todoItem.IsDeleted)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        return todoItem;
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
