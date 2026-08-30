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
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    // BR-011: Soft-delete edilmiş görevler listelenmez
    // Şimdilik sadece owner'ın görevleri; EPIC 5'te TaskShare ile paylaşılan görevler de eklenecek
    public async Task<List<TodoItem>> GetAccessibleByUserAsync(Guid userId)
    {
        return await _context.TodoItems
            .Where(t => t.OwnerId == userId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    // Çöp kutusu: sadece owner'ın soft-delete edilmiş görevleri
    public async Task<List<TodoItem>> GetDeletedByOwnerAsync(Guid userId)
    {
        return await _context.TodoItems
            .Where(t => t.OwnerId == userId && t.IsDeleted)
            .OrderByDescending(t => t.DeletedAt)
            .ToListAsync();
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

