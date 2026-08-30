using TodoApp.Application.DTOs;

namespace TodoApp.Application.Interfaces;

public interface ITodoItemService
{
    Task<TodoItemResponse> CreateAsync(Guid userId, CreateTodoItemRequest request);
    Task<TodoItemResponse> GetByIdAsync(Guid userId, Guid todoItemId);
    Task<List<TodoItemResponse>> GetAllAsync(Guid userId);
    Task<TodoItemResponse> UpdateAsync(Guid userId, Guid todoItemId, UpdateTodoItemRequest request);
    Task<TodoItemResponse> CompleteAsync(Guid userId, Guid todoItemId);
    Task DeleteAsync(Guid userId, Guid todoItemId);
    Task<TodoItemResponse> RestoreAsync(Guid userId, Guid todoItemId);
    Task<List<TodoItemResponse>> GetTrashAsync(Guid userId);
}

