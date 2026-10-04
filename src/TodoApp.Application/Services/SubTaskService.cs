using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class SubTaskService : ISubTaskService
{
    private readonly ISubTaskRepository _subTaskRepository;
    private readonly ITaskAuthorizationService _taskAuthorizationService;
    private readonly ITodoItemActivityService _activityService;
    private readonly INotificationService? _notificationService;

    public SubTaskService(
        ISubTaskRepository subTaskRepository,
        ITaskAuthorizationService taskAuthorizationService,
        ITodoItemActivityService activityService,
        INotificationService? notificationService = null)
    {
        _subTaskRepository = subTaskRepository;
        _taskAuthorizationService = taskAuthorizationService;
        _activityService = activityService;
        _notificationService = notificationService;
    }

    public async Task<SubTaskResponse> CreateAsync(
        Guid userId,
        Guid taskId,
        CreateSubTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ValidationException("Alt görev başlığı boş olamaz.");
        }

        // BR-012, BR-020 & BR-029: Sahip veya Paylaşılan alt görev ekleyebilir, silinmiş göreve eklenemez
        var task = await _taskAuthorizationService.EnsureCanManageSubTasksAsync(taskId, userId);

        var subTaskCount = await _subTaskRepository.CountByTaskIdAsync(taskId);
        if (subTaskCount >= 50)
        {
            throw new ValidationException("Bir göreve en fazla 50 adet alt görev eklenebilir.");
        }

        var subTask = new SubTask
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Title = request.Title.Trim(),
            Status = SubTaskStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        await _subTaskRepository.AddAsync(subTask);
        await _subTaskRepository.SaveChangesAsync();

        await _activityService.LogActivityAsync(
            taskId,
            userId,
            "Alt Görev Eklendi",
            $"'{subTask.Title}' adlı alt görev eklendi."
        );

        if (_notificationService != null && task != null)
        {
            if (task.TaskShares != null)
            {
                foreach (var share in task.TaskShares)
                {
                    if (share.UserId != userId)
                    {
                        await _notificationService.SendTaskUpdatedAsync(share.UserId, taskId, "Alt Görev Eklendi", $"'{task.Title}' görevine yeni bir alt görev eklendi: '{subTask.Title}'");
                    }
                }
            }
            if (userId != task.OwnerId)
            {
                await _notificationService.SendTaskUpdatedAsync(task.OwnerId, taskId, "Alt Görev Eklendi", $"'{task.Title}' görevine yeni bir alt görev eklendi: '{subTask.Title}'");
            }
        }

        return MapToResponse(subTask);
    }

    public async Task<List<SubTaskResponse>> GetByTaskIdAsync(Guid userId, Guid taskId)
    {
        // BR-018, BR-020 & BR-029: Sahip veya Paylaşılan listeleyebilir (üst görev silinmemişse)
        await _taskAuthorizationService.EnsureCanReadAsync(taskId, userId);

        var subTasks = await _subTaskRepository.GetByTaskIdAsync(taskId);
        return subTasks.Select(MapToResponse).ToList();
    }

    public async Task<SubTaskResponse> CompleteAsync(Guid userId, Guid subTaskId)
    {
        // BR-017, BR-020 & BR-029: Sahip veya Paylaşılan alt görevi tamamlayabilir
        var subTask = await _taskAuthorizationService.EnsureCanCompleteSubTaskAsync(subTaskId, userId);

        subTask.Status = subTask.Status == SubTaskStatus.Completed
            ? SubTaskStatus.Open
            : SubTaskStatus.Completed;

        await _subTaskRepository.SaveChangesAsync();

        var actionText = subTask.Status == SubTaskStatus.Completed ? "Alt Görev Tamamlandı" : "Alt Görev Açıldı";
        await _activityService.LogActivityAsync(
            subTask.TaskId,
            userId,
            actionText,
            $"'{subTask.Title}' adlı alt görevin durumu değiştirildi."
        );

        if (_notificationService != null && subTask.Task != null)
        {
            var task = subTask.Task;
            if (task.TaskShares != null)
            {
                foreach (var share in task.TaskShares)
                {
                    if (share.UserId != userId)
                    {
                        await _notificationService.SendTaskUpdatedAsync(share.UserId, task.Id, actionText, $"'{task.Title}' görevindeki '{subTask.Title}' adlı alt görevin durumu değiştirildi.");
                    }
                }
            }
            if (userId != task.OwnerId)
            {
                await _notificationService.SendTaskUpdatedAsync(task.OwnerId, task.Id, actionText, $"'{task.Title}' görevindeki '{subTask.Title}' adlı alt görevin durumu değiştirildi.");
            }
        }

        return MapToResponse(subTask);
    }

    public async Task DeleteAsync(Guid userId, Guid subTaskId)
    {
        // BR-020, BR-026 & BR-029: YALNIZCA görev sahibi silebilir! Paylaşılan kullanıcı silemez (404 döner)
        var subTask = await _taskAuthorizationService.EnsureCanDeleteSubTaskAsync(subTaskId, userId);

        _subTaskRepository.Delete(subTask);
        await _subTaskRepository.SaveChangesAsync();

        await _activityService.LogActivityAsync(
            subTask.TaskId,
            userId,
            "Alt Görev Silindi",
            $"'{subTask.Title}' adlı alt görev silindi."
        );
    }

    private static SubTaskResponse MapToResponse(SubTask subTask)
    {
        return new SubTaskResponse
        {
            Id = subTask.Id,
            TaskId = subTask.TaskId,
            Title = subTask.Title,
            Status = subTask.Status.ToString(),
            CreatedAt = subTask.CreatedAt,
            UpdatedAt = subTask.UpdatedAt
        };
    }
}
