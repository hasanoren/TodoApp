using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class TodoListService : ITodoListService
{
    private readonly ITodoListRepository _todoListRepository;
    private readonly ILogger<TodoListService> _logger;

    public TodoListService(
        ITodoListRepository todoListRepository,
        ILogger<TodoListService>? logger = null)
    {
        _todoListRepository = todoListRepository;
        _logger = logger ?? NullLogger<TodoListService>.Instance;
    }

    public async Task<TodoListResponse> CreateAsync(Guid userId, CreateTodoListRequest request)
    {
        var todoList = new TodoList
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Name = request.Name,
            ColorCode = request.ColorCode,
            CreatedAt = DateTime.UtcNow
        };

        await _todoListRepository.AddAsync(todoList);
        await _todoListRepository.SaveChangesAsync();

        _logger.LogInformation("Yeni liste oluşturuldu. ListId: {ListId}, OwnerId: {OwnerId}", todoList.Id, userId);

        return MapToResponse(todoList);
    }

    public async Task<TodoListResponse> GetByIdAsync(Guid userId, Guid listId)
    {
        var todoList = await _todoListRepository.GetByIdAsync(listId);

        if (todoList == null || todoList.OwnerId != userId)
        {
            throw new ValidationException("Liste bulunamadı veya erişim yetkiniz yok.");
        }

        return MapToResponse(todoList);
    }

    public async Task<List<TodoListResponse>> GetAllAsync(Guid userId)
    {
        var lists = await _todoListRepository.GetByUserIdAsync(userId);
        return lists.Select(MapToResponse).ToList();
    }

    public async Task<TodoListResponse> UpdateAsync(Guid userId, Guid listId, UpdateTodoListRequest request)
    {
        var todoList = await _todoListRepository.GetByIdAsync(listId);

        if (todoList == null || todoList.OwnerId != userId)
        {
            throw new ValidationException("Liste bulunamadı veya erişim yetkiniz yok.");
        }

        todoList.Name = request.Name;
        todoList.ColorCode = request.ColorCode;

        await _todoListRepository.SaveChangesAsync();

        return MapToResponse(todoList);
    }

    public async Task DeleteAsync(Guid userId, Guid listId)
    {
        var todoList = await _todoListRepository.GetByIdAsync(listId);

        if (todoList == null || todoList.OwnerId != userId)
        {
            throw new ValidationException("Liste bulunamadı veya erişim yetkiniz yok.");
        }

        todoList.IsDeleted = true;
        todoList.DeletedByUserId = userId;
        todoList.DeletedAt = DateTime.UtcNow;

        // Cascade soft delete to tasks
        foreach (var task in todoList.TodoItems)
        {
            task.IsDeleted = true;
            task.DeletedByUserId = userId;
            task.DeletedAt = DateTime.UtcNow;
        }

        await _todoListRepository.SaveChangesAsync();

        _logger.LogInformation("Liste silindi. ListId: {ListId}, OwnerId: {OwnerId}", listId, userId);
    }

    private static TodoListResponse MapToResponse(TodoList list)
    {
        return new TodoListResponse
        {
            Id = list.Id,
            Name = list.Name,
            ColorCode = list.ColorCode,
            OwnerId = list.OwnerId,
            CreatedAt = list.CreatedAt
        };
    }
}

