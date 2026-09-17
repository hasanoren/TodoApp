using TodoApp.Domain.Entities;

namespace TodoApp.Application.Interfaces;

public interface ITodoListRepository
{
    Task<TodoList?> GetByIdAsync(Guid id);
    Task<List<TodoList>> GetByUserIdAsync(Guid userId);
    Task AddAsync(TodoList todoList);
    void Delete(TodoList todoList);
    Task SaveChangesAsync();
}

