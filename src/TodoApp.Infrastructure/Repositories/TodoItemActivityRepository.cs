using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Infrastructure.Data;

namespace TodoApp.Infrastructure.Repositories;

public class TodoItemActivityRepository : ITodoItemActivityRepository
{
    private readonly ApplicationDbContext _context;

    public TodoItemActivityRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(TodoItemActivity activity)
    {
        await _context.TodoItemActivities.AddAsync(activity);
    }

    public async Task<List<TodoItemActivity>> GetByTaskIdAsync(Guid taskId)
    {
        return await _context.TodoItemActivities
            .Include(a => a.User)
            .Where(a => a.TodoItemId == taskId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}

