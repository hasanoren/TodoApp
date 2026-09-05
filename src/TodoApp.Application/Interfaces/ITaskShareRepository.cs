using TodoApp.Domain.Entities;

namespace TodoApp.Application.Interfaces;

public interface ITaskShareRepository
{
    Task<TaskShare?> GetAsync(Guid taskId, Guid userId);
    Task<List<TaskShare>> GetByTaskIdAsync(Guid taskId);
    Task<bool> IsSharedWithUserAsync(Guid taskId, Guid userId);
    Task AddAsync(TaskShare taskShare);
    void Remove(TaskShare taskShare);
    Task SaveChangesAsync();
}

