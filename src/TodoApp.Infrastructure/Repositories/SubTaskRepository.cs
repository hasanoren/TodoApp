using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Infrastructure.Data;

namespace TodoApp.Infrastructure.Repositories;

public class SubTaskRepository : ISubTaskRepository
{
    private readonly ApplicationDbContext _context;

    public SubTaskRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SubTask?> GetByIdAsync(Guid id)
    {
        // BR-020: Erişim kontrolü üst Task üzerinden yapılacağı için Task navigation'ı dahil edilir
        return await _context.SubTasks
            .Include(st => st.Task)
            .FirstOrDefaultAsync(st => st.Id == id);
    }

    public async Task<List<SubTask>> GetByTaskIdAsync(Guid taskId)
    {
        return await _context.SubTasks
            .Where(st => st.TaskId == taskId)
            .OrderBy(st => st.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(SubTask subTask)
    {
        await _context.SubTasks.AddAsync(subTask);
    }

    public void Delete(SubTask subTask)
    {
        _context.SubTasks.Remove(subTask);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}

