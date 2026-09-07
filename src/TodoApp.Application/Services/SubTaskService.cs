using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class SubTaskService : ISubTaskService
{
    private readonly ISubTaskRepository _subTaskRepository;
    private readonly ITaskAuthorizationService _taskAuthorizationService;

    public SubTaskService(
        ISubTaskRepository subTaskRepository,
        ITaskAuthorizationService taskAuthorizationService)
    {
        _subTaskRepository = subTaskRepository;
        _taskAuthorizationService = taskAuthorizationService;
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
        await _taskAuthorizationService.EnsureCanManageSubTasksAsync(taskId, userId);

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

        return MapToResponse(subTask);
    }

    public async Task DeleteAsync(Guid userId, Guid subTaskId)
    {
        // BR-020, BR-026 & BR-029: YALNIZCA görev sahibi silebilir! Paylaşılan kullanıcı silemez (404 döner)
        var subTask = await _taskAuthorizationService.EnsureCanDeleteSubTaskAsync(subTaskId, userId);

        _subTaskRepository.Delete(subTask);
        await _subTaskRepository.SaveChangesAsync();
    }

    private static SubTaskResponse MapToResponse(SubTask subTask)
    {
        return new SubTaskResponse
        {
            Id = subTask.Id,
            TaskId = subTask.TaskId,
            Title = subTask.Title,
            Status = subTask.Status.ToString(),
            CreatedAt = subTask.CreatedAt
        };
    }
}
