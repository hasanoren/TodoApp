using TodoApp.Application.DTOs;

namespace TodoApp.Application.Interfaces;

public interface ISubTaskService
{
    Task<SubTaskResponse> CreateAsync(Guid userId, Guid taskId, CreateSubTaskRequest request);
    Task<List<SubTaskResponse>> GetByTaskIdAsync(Guid userId, Guid taskId);
    Task<SubTaskResponse> CompleteAsync(Guid userId, Guid subTaskId);
    Task DeleteAsync(Guid userId, Guid subTaskId);
}

