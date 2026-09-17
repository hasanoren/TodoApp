using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Infrastructure.Data;

namespace TodoApp.Infrastructure.Repositories;

public class TodoListRepository : ITodoListRepository
{
    private readonly ApplicationDbContext _context;

    public TodoListRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TodoList?> GetByIdAsync(Guid id)
    {
        return await _context.TodoLists
            .Include(tl => tl.TodoItems.Where(ti => !ti.IsDeleted))
            .FirstOrDefaultAsync(tl => tl.Id == id);
    }

    public async Task<List<TodoList>> GetByUserIdAsync(Guid userId)
    {
        return await _context.TodoLists
            .AsNoTracking()
            .Where(tl => tl.OwnerId == userId)
            .OrderByDescending(tl => tl.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(TodoList todoList)
    {
        await _context.TodoLists.AddAsync(todoList);
    }

    public void Delete(TodoList todoList)
    {
        _context.TodoLists.Remove(todoList);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}

