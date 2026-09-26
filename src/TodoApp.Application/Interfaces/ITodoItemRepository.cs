using TodoApp.Application.DTOs;
using TodoApp.Domain.Entities;

namespace TodoApp.Application.Interfaces;

public interface ITodoItemRepository
{
    Task<TodoItem?> GetByIdAsync(Guid id, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<(List<TodoItem> Items, int TotalCount)> GetAccessibleByUserAsync(Guid userId, TodoItemFilterDto filter, CancellationToken cancellationToken = default);
    Task<(List<TodoItem> Items, int TotalCount)> GetAccessibleByUserAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<(List<TodoItem> Items, int TotalCount)> GetDeletedByOwnerAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(TodoItem todoItem, CancellationToken cancellationToken = default);
    void Delete(TodoItem todoItem);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
