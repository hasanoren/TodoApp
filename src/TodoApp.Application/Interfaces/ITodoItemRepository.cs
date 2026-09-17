using TodoApp.Application.DTOs;
using TodoApp.Domain.Entities;

namespace TodoApp.Application.Interfaces;

public interface ITodoItemRepository
{
    Task<TodoItem?> GetByIdAsync(Guid id, bool includeDeleted = false);
    Task<(List<TodoItem> Items, int TotalCount)> GetAccessibleByUserAsync(Guid userId, TodoItemFilterDto filter);
    Task<(List<TodoItem> Items, int TotalCount)> GetAccessibleByUserAsync(Guid userId, int page, int pageSize);
    Task<(List<TodoItem> Items, int TotalCount)> GetDeletedByOwnerAsync(Guid userId, int page, int pageSize);
    Task AddAsync(TodoItem todoItem);
    void Delete(TodoItem todoItem);
    Task SaveChangesAsync();
}

