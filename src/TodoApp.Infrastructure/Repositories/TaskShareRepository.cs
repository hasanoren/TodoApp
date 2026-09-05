using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Infrastructure.Data;

namespace TodoApp.Infrastructure.Repositories;

public class TaskShareRepository : ITaskShareRepository
{
    private readonly ApplicationDbContext _context;

    public TaskShareRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TaskShare?> GetAsync(Guid taskId, Guid userId)
    {
        return await _context.TaskShares
            .Include(ts => ts.User)
            .FirstOrDefaultAsync(ts => ts.TaskId == taskId && ts.UserId == userId);
    }

    public async Task<List<TaskShare>> GetByTaskIdAsync(Guid taskId)
    {
        return await _context.TaskShares
            .Include(ts => ts.User)
            .Where(ts => ts.TaskId == taskId)
            .OrderBy(ts => ts.SharedAt)
            .ToListAsync();
    }

    public async Task<bool> IsSharedWithUserAsync(Guid taskId, Guid userId)
    {
        return await _context.TaskShares
            .AnyAsync(ts => ts.TaskId == taskId && ts.UserId == userId);
    }

    public async Task AddAsync(TaskShare taskShare)
    {
        await _context.TaskShares.AddAsync(taskShare);
    }

    public void Remove(TaskShare taskShare)
    {
        _context.TaskShares.Remove(taskShare);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}

