using TodoApp.Application.DTOs;

namespace TodoApp.Application.Interfaces;

public interface ITodoListService
{
    Task<TodoListResponse> CreateAsync(Guid userId, CreateTodoListRequest request);
    Task<TodoListResponse> GetByIdAsync(Guid userId, Guid listId);
    Task<List<TodoListResponse>> GetAllAsync(Guid userId);
    Task<TodoListResponse> UpdateAsync(Guid userId, Guid listId, UpdateTodoListRequest request);
    Task DeleteAsync(Guid userId, Guid listId);
}

