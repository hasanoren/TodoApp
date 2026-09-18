using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;

namespace TodoApp.Application.Services;

public class TodoItemActivityService : ITodoItemActivityService
{
    private readonly ITodoItemActivityRepository _activityRepository;
    private readonly ITaskAuthorizationService _taskAuthorizationService;

    public TodoItemActivityService(
        ITodoItemActivityRepository activityRepository,
        ITaskAuthorizationService taskAuthorizationService)
    {
        _activityRepository = activityRepository;
        _taskAuthorizationService = taskAuthorizationService;
    }

    public async Task LogActivityAsync(Guid taskId, Guid userId, string action, string details)
    {
        var activity = new TodoItemActivity
        {
            Id = Guid.NewGuid(),
            TodoItemId = taskId,
            UserId = userId,
            Action = action,
            Details = details,
            CreatedAt = DateTime.UtcNow
        };

        await _activityRepository.AddAsync(activity);
        await _activityRepository.SaveChangesAsync();
    }

    public async Task<List<TodoItemActivityResponse>> GetActivitiesByTaskIdAsync(Guid taskId, Guid userId)
    {
        // Görevi okuma yetkisi var mı? (BR-016: Owner ve Shared Users okuyabilir)
        await _taskAuthorizationService.EnsureCanReadAsync(taskId, userId);

        var activities = await _activityRepository.GetByTaskIdAsync(taskId);

        return activities.Select(a => new TodoItemActivityResponse
        {
            Id = a.Id,
            UserId = a.UserId,
            UserEmail = a.User?.Email ?? "Silinmiş Kullanıcı",
            Action = a.Action,
            Details = a.Details,
            CreatedAt = a.CreatedAt
        }).ToList();
    }
}

