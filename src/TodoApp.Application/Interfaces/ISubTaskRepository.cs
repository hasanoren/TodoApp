using TodoApp.Domain.Entities;

namespace TodoApp.Application.Interfaces;

public interface ISubTaskRepository
{
    Task<SubTask?> GetByIdAsync(Guid id);
    Task<List<SubTask>> GetByTaskIdAsync(Guid taskId);
    Task AddAsync(SubTask subTask);
    void Delete(SubTask subTask);
    Task SaveChangesAsync();
}

