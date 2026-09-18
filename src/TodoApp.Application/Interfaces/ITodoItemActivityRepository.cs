using TodoApp.Domain.Entities;

namespace TodoApp.Application.Interfaces;

public interface ITodoItemActivityRepository
{
    Task AddAsync(TodoItemActivity activity);
    Task<List<TodoItemActivity>> GetByTaskIdAsync(Guid taskId);
    Task SaveChangesAsync();
}

