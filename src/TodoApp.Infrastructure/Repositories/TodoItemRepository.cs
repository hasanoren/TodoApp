using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Infrastructure.Data;

namespace TodoApp.Infrastructure.Repositories;

public class TodoItemRepository : ITodoItemRepository
{
    private readonly ApplicationDbContext _context;

    public TodoItemRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TodoItem?> GetByIdAsync(Guid id)
    {
        return await _context.TodoItems
            .Include(t => t.SubTasks)
            .Include(t => t.TodoItemTags)
                .ThenInclude(tit => tit.Tag)
            .Include(t => t.TaskShares)
                .ThenInclude(ts => ts.User)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    // BR-011: Soft-delete edilmiş görevler listelenmez
    // Liste görünümü için hafif sorgu (SubTasks dahil edilmez, sadece Tag'ler ve Paylaşılanlar dahil edilir)
    // Hem kendi görevleri hem kendisiyle paylaşılan görevler gelir
    public async Task<(List<TodoItem> Items, int TotalCount)> GetAccessibleByUserAsync(Guid userId, int page, int pageSize)
    {
        var query = _context.TodoItems
            .Where(t => (t.OwnerId == userId || t.TaskShares.Any(ts => ts.UserId == userId)) && !t.IsDeleted);

        var totalCount = await query.CountAsync();

        var items = await query
            .Include(t => t.TodoItemTags)
                .ThenInclude(tit => tit.Tag)
            .Include(t => t.TaskShares)
                .ThenInclude(ts => ts.User)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    // Çöp kutusu: sadece owner'ın soft-delete edilmiş görevleri
    public async Task<(List<TodoItem> Items, int TotalCount)> GetDeletedByOwnerAsync(Guid userId, int page, int pageSize)
    {
        var query = _context.TodoItems
            .Where(t => t.OwnerId == userId && t.IsDeleted);

        var totalCount = await query.CountAsync();

        var items = await query
            .Include(t => t.SubTasks)
            .Include(t => t.TodoItemTags)
                .ThenInclude(tit => tit.Tag)
            .OrderByDescending(t => t.DeletedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task AddAsync(TodoItem todoItem)
    {
        await _context.TodoItems.AddAsync(todoItem);
    }

    // Hard delete (BR-008a)
    public void Delete(TodoItem todoItem)
    {
        _context.TodoItems.Remove(todoItem);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}

