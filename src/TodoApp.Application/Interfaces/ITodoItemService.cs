using TodoApp.Application.DTOs;

namespace TodoApp.Application.Interfaces;

public interface ITodoItemService
{
    Task<TodoItemResponse> CreateAsync(Guid userId, CreateTodoItemRequest request, CancellationToken cancellationToken = default);
    Task<TodoItemResponse> GetByIdAsync(Guid userId, Guid todoItemId, CancellationToken cancellationToken = default);
    Task<PaginatedResponse<TodoItemResponse>> GetAllAsync(Guid userId, TodoItemFilterDto filter, CancellationToken cancellationToken = default);
    Task<PaginatedResponse<TodoItemResponse>> GetAllAsync(Guid userId, PaginatedRequest request, CancellationToken cancellationToken = default);
    Task<TodoItemResponse> UpdateAsync(Guid userId, Guid todoItemId, UpdateTodoItemRequest request, CancellationToken cancellationToken = default);
    Task<TodoItemResponse> CompleteAsync(Guid userId, Guid todoItemId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid todoItemId, CancellationToken cancellationToken = default);
    Task PermanentDeleteAsync(Guid userId, Guid todoItemId, CancellationToken cancellationToken = default);
    Task<TodoItemResponse> RestoreAsync(Guid userId, Guid todoItemId, CancellationToken cancellationToken = default);
    Task<PaginatedResponse<TodoItemResponse>> GetTrashAsync(Guid userId, PaginatedRequest request, CancellationToken cancellationToken = default);
}

