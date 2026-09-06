using TodoApp.Domain.Entities;

namespace TodoApp.Application.Interfaces;

public interface ITagRepository
{
    Task<Tag?> GetByIdAsync(Guid id);
    Task<Tag?> GetByNameAsync(string name);
    Task<List<Tag>> GetAllAsync();
    Task<List<Tag>> GetTagsByTodoItemIdAsync(Guid todoItemId);
    Task<(List<TodoItem> Items, int TotalCount)> GetTodoItemsByTagIdAsync(Guid userId, Guid tagId, int page, int pageSize);
    Task AddAsync(Tag tag);
    Task<TodoItemTag?> GetTodoItemTagAsync(Guid todoItemId, Guid tagId);
    Task AddTodoItemTagAsync(TodoItemTag todoItemTag);
    void RemoveTodoItemTag(TodoItemTag todoItemTag);
    Task SaveChangesAsync();
}

