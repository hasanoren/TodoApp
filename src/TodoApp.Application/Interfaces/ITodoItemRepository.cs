using TodoApp.Domain.Entities;

namespace TodoApp.Application.Interfaces;

public interface ITodoItemRepository
{
    Task<TodoItem?> GetByIdAsync(Guid id);
    Task<List<TodoItem>> GetAccessibleByUserAsync(Guid userId);
    Task<List<TodoItem>> GetDeletedByOwnerAsync(Guid userId);
    Task AddAsync(TodoItem todoItem);
    void Delete(TodoItem todoItem);
    Task SaveChangesAsync();
}

