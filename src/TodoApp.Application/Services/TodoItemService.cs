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

        return MapToResponse(todoItem);
    }

    public async Task<TodoItemResponse> GetByIdAsync(Guid userId, Guid todoItemId)
    {
        var todoItem = await GetAuthorizedTodoItemAsync(userId, todoItemId);
        return MapToResponse(todoItem);
    }

    public async Task<List<TodoItemResponse>> GetAllAsync(Guid userId)
    {
        // BR-011: IsDeleted=false filtresi repository'de uygulanıyor
        var items = await _todoItemRepository.GetAccessibleByUserAsync(userId);
        return items.Select(MapToResponse).ToList();
    }

    public async Task<TodoItemResponse> UpdateAsync(
        Guid userId, Guid todoItemId, UpdateTodoItemRequest request)
    {
        var todoItem = await GetAuthorizedTodoItemAsync(userId, todoItemId);

        todoItem.Title = request.Title;
        todoItem.Description = request.Description;
        todoItem.DueDate = request.DueDate;

        await _todoItemRepository.SaveChangesAsync();

        return MapToResponse(todoItem);
    }

    public async Task<TodoItemResponse> CompleteAsync(Guid userId, Guid todoItemId)
    {
        var todoItem = await GetAuthorizedTodoItemAsync(userId, todoItemId);

        // BR-015: CompletedByUserId ve CompletedAt set edilir, paylaşım kalksa da korunur
        todoItem.Status = TodoItemStatus.Completed;
        todoItem.CompletedByUserId = userId;
        todoItem.CompletedAt = DateTime.UtcNow;

        await _todoItemRepository.SaveChangesAsync();

        return MapToResponse(todoItem);
    }

    public async Task DeleteAsync(Guid userId, Guid todoItemId)
    {
        var todoItem = await GetAuthorizedTodoItemAsync(userId, todoItemId);

        // BR-009: Tamamlanmış görev de silinebilir — Status kontrolü yapılmaz

        if (todoItem.OwnerId == userId)
        {
            // BR-008a: Owner siliyorsa → hard delete, restore edilemez
            _todoItemRepository.Delete(todoItem);
        }
        else
        {
            // BR-008b: Paylaşılan kullanıcı siliyorsa → soft delete (EPIC 5'te aktif olacak)
            // Şimdilik sadece owner silebilir; bu dal TaskShare eklenince çalışacak
            todoItem.IsDeleted = true;
            todoItem.DeletedByUserId = userId;
            todoItem.DeletedAt = DateTime.UtcNow;
        }

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

        return MapToResponse(todoItem);
    }

    public async Task<List<TodoItemResponse>> GetTrashAsync(Guid userId)
    {
        var items = await _todoItemRepository.GetDeletedByOwnerAsync(userId);
        return items.Select(MapToResponse).ToList();
    }

    // --- Yardımcı Metotlar ---

    /// <summary>
    /// Görev ID'sine göre görev getirir ve kullanıcının yetkisini kontrol eder.
    /// BR-029: Yetkisiz erişimde 404 döner (403 değil).
    /// Şimdilik sadece owner kontrolü; EPIC 5'te TaskShare kontrolü eklenecek.
    /// </summary>
    private async Task<TodoItem> GetAuthorizedTodoItemAsync(Guid userId, Guid todoItemId)
    {
        var todoItem = await _todoItemRepository.GetByIdAsync(todoItemId);

        // BR-029: Owner veya TaskShare'de kayıtlı olmayan kullanıcı → 404
        if (todoItem is null || todoItem.OwnerId != userId)
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

    private static TodoItemResponse MapToResponse(TodoItem todoItem)
    {
        return new TodoItemResponse
        {
            Id = todoItem.Id,
            Title = todoItem.Title,
            Description = todoItem.Description,
            DueDate = todoItem.DueDate,
            Status = todoItem.Status.ToString(),
            OwnerId = todoItem.OwnerId,
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
            }).ToList() ?? new List<TagResponse>()
        };
    }
}

