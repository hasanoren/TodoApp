using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Infrastructure.Data;

namespace TodoApp.Infrastructure.Repositories;

public class TagRepository : ITagRepository
{
    private readonly ApplicationDbContext _context;

    public TagRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Tag?> GetByIdAsync(Guid id)
    {
        return await _context.Tags.FindAsync(id);
    }

    public async Task<Tag?> GetByNameAsync(string name)
    {
        // BR-021: Case-insensitive etiket arama
        var normalized = name.Trim().ToLower();
        return await _context.Tags
            .FirstOrDefaultAsync(t => t.Name.ToLower() == normalized);
    }

    public async Task<List<Tag>> GetAllAsync()
    {
        return await _context.Tags
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<Tag>> GetTagsByTodoItemIdAsync(Guid todoItemId)
    {
        return await _context.TodoItemTags
            .Where(tit => tit.TodoItemId == todoItemId)
            .Select(tit => tit.Tag)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<TodoItem>> GetTodoItemsByTagIdAsync(Guid userId, Guid tagId)
    {
        return await _context.TodoItemTags
            .Where(tit => tit.TagId == tagId && tit.TodoItem.OwnerId == userId && !tit.TodoItem.IsDeleted)
            .Include(tit => tit.TodoItem)
                .ThenInclude(t => t.TodoItemTags)
                    .ThenInclude(tit2 => tit2.Tag)
            .Select(tit => tit.TodoItem)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Tag tag)
    {
        await _context.Tags.AddAsync(tag);
    }

    public async Task<TodoItemTag?> GetTodoItemTagAsync(Guid todoItemId, Guid tagId)
    {
        return await _context.TodoItemTags
            .FirstOrDefaultAsync(tit => tit.TodoItemId == todoItemId && tit.TagId == tagId);
    }

    public async Task AddTodoItemTagAsync(TodoItemTag todoItemTag)
    {
        await _context.TodoItemTags.AddAsync(todoItemTag);
    }

    public void RemoveTodoItemTag(TodoItemTag todoItemTag)
    {
        _context.TodoItemTags.Remove(todoItemTag);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}

