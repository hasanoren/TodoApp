using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class SubTaskService : ISubTaskService
{
    private readonly ISubTaskRepository _subTaskRepository;
    private readonly ITodoItemRepository _todoItemRepository;

    public SubTaskService(
        ISubTaskRepository subTaskRepository,
        ITodoItemRepository todoItemRepository)
    {
        _subTaskRepository = subTaskRepository;
        _todoItemRepository = todoItemRepository;
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

        var parentTask = await _todoItemRepository.GetByIdAsync(taskId);

        // BR-020 & BR-029: Yetki kontrolü parent Task üzerinden yapılır, yetkisiz ise 404
        if (parentTask is null || parentTask.OwnerId != userId)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        // BR-012: Silinmiş (soft-delete) bir Task'a yeni SubTask eklenemez
        if (parentTask.IsDeleted)
        {
            throw new ValidationException("Silinmiş bir göreve alt görev eklenemez.");
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

        return MapToResponse(subTask);
    }

    public async Task<List<SubTaskResponse>> GetByTaskIdAsync(Guid userId, Guid taskId)
    {
        var parentTask = await _todoItemRepository.GetByIdAsync(taskId);

        // BR-020 & BR-029: Yetki kontrolü parent Task üzerinden
        if (parentTask is null || parentTask.OwnerId != userId)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        // BR-018: Üst görev soft-delete ise alt görevler de erişilemez
        if (parentTask.IsDeleted)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        var subTasks = await _subTaskRepository.GetByTaskIdAsync(taskId);
        return subTasks.Select(MapToResponse).ToList();
    }

    public async Task<SubTaskResponse> CompleteAsync(Guid userId, Guid subTaskId)
    {
        var subTask = await _subTaskRepository.GetByIdAsync(subTaskId);

        // BR-020 & BR-029: Erişim kontrolü üst Task üzerinden yapılır
        if (subTask is null || subTask.Task.OwnerId != userId)
        {
            throw new NotFoundException("Alt görev bulunamadı.");
        }

        // BR-018: Üst görev soft-delete ise erişilemez
        if (subTask.Task.IsDeleted)
        {
            throw new NotFoundException("Alt görev bulunamadı.");
        }

        // BR-017: Alt görevin durumu bağımsız değişir, üst göreve dokunulmaz
        subTask.Status = subTask.Status == SubTaskStatus.Completed
            ? SubTaskStatus.Open
            : SubTaskStatus.Completed;

        await _subTaskRepository.SaveChangesAsync();

        return MapToResponse(subTask);
    }

    public async Task DeleteAsync(Guid userId, Guid subTaskId)
    {
        var subTask = await _subTaskRepository.GetByIdAsync(subTaskId);

        // BR-020 & BR-029: Erişim kontrolü üst Task üzerinden yapılır
        if (subTask is null || subTask.Task.OwnerId != userId)
        {
            throw new NotFoundException("Alt görev bulunamadı.");
        }

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

