using TodoApp.Application.DTOs;

namespace TodoApp.Application.Interfaces;

public interface ITodoItemActivityService
{
    Task LogActivityAsync(Guid taskId, Guid userId, string action, string details);
    Task<List<TodoItemActivityResponse>> GetActivitiesByTaskIdAsync(Guid taskId, Guid userId);
}

