using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class TodoItemService : ITodoItemService
{
    private readonly ITodoItemRepository _todoItemRepository;
    private readonly ITodoListRepository? _todoListRepository;
    private readonly ITaskAuthorizationService _taskAuthorizationService;
    private readonly INotificationService _notificationService;
    private readonly ITodoItemActivityService _activityService;
    private readonly ILogger<TodoItemService> _logger;

    public TodoItemService(
        ITodoItemRepository todoItemRepository,
        ITaskAuthorizationService taskAuthorizationService,
        INotificationService notificationService,
        ITodoItemActivityService activityService,
        ITodoListRepository? todoListRepository = null,
        ILogger<TodoItemService>? logger = null)
    {
        _todoItemRepository = todoItemRepository;
        _taskAuthorizationService = taskAuthorizationService;
        _notificationService = notificationService;
        _activityService = activityService;
        _todoListRepository = todoListRepository;
        _logger = logger ?? NullLogger<TodoItemService>.Instance;
    }

    public async Task<TodoItemResponse> CreateAsync(Guid userId, CreateTodoItemRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TodoListId.HasValue && _todoListRepository != null)
        {
            var list = await _todoListRepository.GetByIdAsync(request.TodoListId.Value);
            if (list == null || list.OwnerId != userId || list.IsDeleted)
            {
                throw new ValidationException("Belirtilen görev listesi bulunamadı veya erişim yetkiniz yok.");
            }
        }

        var todoItem = new TodoItem
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,           // BR-006: owner NOT NULL
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate,
            Status = TodoItemStatus.Open,
            Priority = request.Priority,
            TodoListId = request.TodoListId,
            CreatedAt = DateTime.UtcNow
        };

        await _todoItemRepository.AddAsync(todoItem, cancellationToken);
        await _todoItemRepository.SaveChangesAsync(cancellationToken);

        await _activityService.LogActivityAsync(todoItem.Id, userId, "Oluşturuldu", "Görev oluşturuldu.");

        _logger.LogInformation("Görev başarıyla oluşturuldu. TaskId: {TaskId}, OwnerId: {OwnerId}, Title: {Title}", todoItem.Id, userId, todoItem.Title);

        return MapToResponse(todoItem, userId);
    }

    public async Task<TodoItemResponse> GetByIdAsync(Guid userId, Guid todoItemId, CancellationToken cancellationToken = default)
    {
        var todoItem = await _taskAuthorizationService.EnsureCanReadAsync(todoItemId, userId);
        return MapToResponse(todoItem, userId);
    }

    public async Task<PaginatedResponse<TodoItemResponse>> GetAllAsync(Guid userId, PaginatedRequest request, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, PaginatedRequest.MaxPageSize);

        // BR-011: IsDeleted=false filtresi repository'de uygulanıyor
        var (items, totalCount) = await _todoItemRepository.GetAccessibleByUserAsync(userId, page, pageSize, cancellationToken);
        var mappedItems = (items ?? new List<TodoItem>()).Select(item => MapToResponse(item, userId)).ToList();

        return new PaginatedResponse<TodoItemResponse>(mappedItems, totalCount, page, pageSize);
    }

    public async Task<PaginatedResponse<TodoItemResponse>> GetAllAsync(Guid userId, TodoItemFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, PaginatedRequest.MaxPageSize);

        // BR-011: IsDeleted=false filtresi repository'de uygulanıyor
        var (items, totalCount) = await _todoItemRepository.GetAccessibleByUserAsync(userId, filter, cancellationToken);
        var mappedItems = (items ?? new List<TodoItem>()).Select(item => MapToResponse(item, userId)).ToList();

        return new PaginatedResponse<TodoItemResponse>(mappedItems, totalCount, page, pageSize);
    }

    public async Task<TodoItemResponse> UpdateAsync(
        Guid userId, Guid todoItemId, UpdateTodoItemRequest request, CancellationToken cancellationToken = default)
    {
        var todoItem = await _taskAuthorizationService.EnsureCanModifyAsync(todoItemId, userId);

        if (request.TodoListId.HasValue && request.TodoListId != todoItem.TodoListId && _todoListRepository != null)
        {
            var list = await _todoListRepository.GetByIdAsync(request.TodoListId.Value);
            if (list == null || list.OwnerId != todoItem.OwnerId || list.OwnerId != userId || list.IsDeleted)
            {
                throw new ValidationException("Belirtilen görev listesi bulunamadı veya erişim yetkiniz yok.");
            }
        }

        todoItem.Title = request.Title;
        todoItem.Description = request.Description;
        todoItem.DueDate = request.DueDate;
        todoItem.Priority = request.Priority;
        todoItem.TodoListId = request.TodoListId;

        await _todoItemRepository.SaveChangesAsync(cancellationToken);

        await _activityService.LogActivityAsync(todoItemId, userId, "Güncellendi", "Görevin detayları güncellendi.");

        if (userId != todoItem.OwnerId)
        {
            await _notificationService.SendNotificationAsync(
                todoItem.OwnerId,
                "Görev Güncellendi",
                $"Paylaştığınız '{todoItem.Title}' adlı görev güncellendi."
            );
        }

        _logger.LogInformation("Görev güncellendi. TaskId: {TaskId}, UserId: {UserId}", todoItemId, userId);

        return MapToResponse(todoItem, userId);
    }

    public async Task<TodoItemResponse> CompleteAsync(Guid userId, Guid todoItemId, CancellationToken cancellationToken = default)
    {
        var todoItem = await _taskAuthorizationService.EnsureCanCompleteAsync(todoItemId, userId);

        if (todoItem.Status == TodoItemStatus.Completed)
        {
            todoItem.Status = TodoItemStatus.Open;
            todoItem.CompletedByUserId = null;
            todoItem.CompletedAt = null;
            await _activityService.LogActivityAsync(todoItemId, userId, "Tekrar Açıldı", "Görev tekrar açıldı (Open).");
        }
        else
        {
            todoItem.Status = TodoItemStatus.Completed;
            todoItem.CompletedByUserId = userId;
            todoItem.CompletedAt = DateTime.UtcNow;
            await _activityService.LogActivityAsync(todoItemId, userId, "Tamamlandı", "Görev tamamlandı.");
        }

        await _todoItemRepository.SaveChangesAsync(cancellationToken);

        if (userId != todoItem.OwnerId)
        {
            await _notificationService.SendNotificationAsync(
                todoItem.OwnerId,
                "Görev Tamamlandı",
                $"Paylaştığınız '{todoItem.Title}' adlı görev tamamlandı."
            );
        }

        _logger.LogInformation("Görev tamamlandı. TaskId: {TaskId}, CompletedByUserId: {UserId}", todoItemId, userId);

        return MapToResponse(todoItem, userId);
    }

    public async Task DeleteAsync(Guid userId, Guid todoItemId, CancellationToken cancellationToken = default)
    {
        // BR-008 & BR-026: Yalnızca görev sahibi silebilir! Paylaşılan kullanıcılar silemez
        var todoItem = await _taskAuthorizationService.EnsureCanDeleteAsync(todoItemId, userId);

        // BR-008: Soft delete uygulanır (çöp kutusuna gider, restore edilebilir)
        todoItem.IsDeleted = true;
        todoItem.DeletedByUserId = userId;
        todoItem.DeletedAt = DateTime.UtcNow;

        await _todoItemRepository.SaveChangesAsync(cancellationToken);

        await _activityService.LogActivityAsync(todoItemId, userId, "Silindi", "Görev çöp kutusuna taşındı.");

        _logger.LogInformation("Görev çöp kutusuna taşındı (soft delete). TaskId: {TaskId}, DeletedByUserId: {UserId}", todoItemId, userId);
    }

    public async Task PermanentDeleteAsync(Guid userId, Guid todoItemId, CancellationToken cancellationToken = default)
    {
        // BR-010, BR-029: Sadece owner kalıcı silebilir (çöp kutusundaki görevler için includeDeleted: true)
        var todoItem = await _taskAuthorizationService.EnsureOwnerAsync(todoItemId, userId, includeDeleted: true);

        if (!todoItem.IsDeleted)
        {
            throw new ValidationException("Yalnızca çöp kutusundaki görevler kalıcı olarak silinebilir.");
        }

        _todoItemRepository.Delete(todoItem);
        await _todoItemRepository.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Görev kalıcı olarak silindi. TaskId: {TaskId}, OwnerId: {OwnerId}", todoItemId, userId);
    }

    public async Task<TodoItemResponse> RestoreAsync(Guid userId, Guid todoItemId, CancellationToken cancellationToken = default)
    {
        // BR-010: Sadece owner restore edebilir (çöp kutusundaki görevler için includeDeleted: true)
        var todoItem = await _taskAuthorizationService.EnsureOwnerAsync(todoItemId, userId, includeDeleted: true);

        if (!todoItem.IsDeleted)
        {
            throw new ValidationException("Bu görev zaten aktif durumda.");
        }

        todoItem.IsDeleted = false;
        todoItem.DeletedByUserId = null;
        todoItem.DeletedAt = null;

        // Senaryo B Koruması: Eğer görev bir listeye aitse ama o liste silinmişse (veya bulunamıyorsa), görevi ana havuza (Inbox) düşür.
        if (todoItem.TodoListId.HasValue && (todoItem.TodoList == null || todoItem.TodoList.IsDeleted))
        {
            var originalListId = todoItem.TodoListId;
            todoItem.TodoListId = null;
            _logger.LogWarning("Görev geri yüklendi ancak ait olduğu liste (ListId: {ListId}) silinmiş olduğu için görev ana havuza (Inbox) taşındı.", originalListId);
        }

        await _todoItemRepository.SaveChangesAsync(cancellationToken);

        await _activityService.LogActivityAsync(todoItemId, userId, "Geri Yüklendi", "Görev çöp kutusundan geri yüklendi.");

        _logger.LogInformation("Görev çöp kutusundan geri yüklendi. TaskId: {TaskId}, OwnerId: {OwnerId}", todoItemId, userId);

        return MapToResponse(todoItem, userId);
    }

    public async Task<PaginatedResponse<TodoItemResponse>> GetTrashAsync(Guid userId, PaginatedRequest request, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, PaginatedRequest.MaxPageSize);

        var (items, totalCount) = await _todoItemRepository.GetDeletedByOwnerAsync(userId, page, pageSize, cancellationToken);
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
            Priority = todoItem.Priority.ToString(),
            TodoListId = todoItem.TodoListId,
            OwnerId = todoItem.OwnerId,
            IsOwner = todoItem.OwnerId == currentUserId,
            CompletedByUserId = todoItem.CompletedByUserId,
            CompletedAt = todoItem.CompletedAt,
            CreatedAt = todoItem.CreatedAt,
            UpdatedAt = todoItem.UpdatedAt,
            IsDeleted = todoItem.IsDeleted,
            DeletedAt = todoItem.DeletedAt,
            SubTasks = todoItem.SubTasks?.Select(st => new SubTaskResponse
            {
                Id = st.Id,
                TaskId = st.TaskId,
                Title = st.Title,
                Status = st.Status.ToString(),
                CreatedAt = st.CreatedAt,
                UpdatedAt = st.UpdatedAt
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
