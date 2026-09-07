using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class TaskAuthorizationService : ITaskAuthorizationService
{
    private readonly ITodoItemRepository _todoItemRepository;
    private readonly ISubTaskRepository _subTaskRepository;

    public TaskAuthorizationService(
        ITodoItemRepository todoItemRepository,
        ISubTaskRepository subTaskRepository)
    {
        _todoItemRepository = todoItemRepository;
        _subTaskRepository = subTaskRepository;
    }

    public async Task<TodoItem> EnsureCanReadAsync(Guid taskId, Guid userId, bool allowTrash = false)
    {
        var task = await _todoItemRepository.GetByIdAsync(taskId);

        if (task is null)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        var isOwner = task.OwnerId == userId;
        var isShared = task.TaskShares != null && task.TaskShares.Any(ts => ts.UserId == userId);

        // BR-029: Yetkisiz kullanıcıya 404 (varlık sızdırmama)
        if (!isOwner && !isShared)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        // BR-011: Soft-delete edilmiş görev aktif listelerde ve detayda görünmez
        if (task.IsDeleted)
        {
            // Sadece owner trash üzerinden görebilir (allowTrash = true)
            if (!allowTrash || !isOwner)
            {
                throw new NotFoundException("Görev bulunamadı.");
            }
        }

        return task;
    }

    public async Task<TodoItem> EnsureCanModifyAsync(Guid taskId, Guid userId)
    {
        // BR-025: Görevi Sahip veya Paylaşılan güncelleyebilir (silinmemiş olmalı)
        return await EnsureCanReadAsync(taskId, userId, allowTrash: false);
    }

    public async Task<TodoItem> EnsureCanCompleteAsync(Guid taskId, Guid userId)
    {
        // BR-025: Görevi Sahip veya Paylaşılan tamamlayabilir (silinmemiş olmalı)
        return await EnsureCanReadAsync(taskId, userId, allowTrash: false);
    }

    public async Task<TodoItem> EnsureCanDeleteAsync(Guid taskId, Guid userId)
    {
        var task = await _todoItemRepository.GetByIdAsync(taskId);

        // BR-008, BR-026, BR-029: YALNIZCA görev sahibi silebilir. Paylaşılan veya yabancı kullanıcı 404 alır.
        if (task is null || task.OwnerId != userId || task.IsDeleted)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        return task;
    }

    public async Task<TodoItem> EnsureOwnerAsync(Guid taskId, Guid userId)
    {
        var task = await _todoItemRepository.GetByIdAsync(taskId);

        // BR-010, BR-013, BR-030: Mutlak sahiplik doğrulaması
        if (task is null || task.OwnerId != userId)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        return task;
    }

    public async Task<TodoItem> EnsureCanManageSubTasksAsync(Guid taskId, Guid userId)
    {
        var task = await _todoItemRepository.GetByIdAsync(taskId);

        if (task is null)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        var isOwner = task.OwnerId == userId;
        var isShared = task.TaskShares != null && task.TaskShares.Any(ts => ts.UserId == userId);

        // BR-020, BR-029: Erişim kontrolü üst task üzerinden
        if (!isOwner && !isShared)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        // BR-012: Silinmiş göreve alt görev eklenemez
        if (task.IsDeleted)
        {
            throw new ValidationException("Silinmiş bir göreve alt görev eklenemez.");
        }

        return task;
    }

    public async Task<SubTask> EnsureCanCompleteSubTaskAsync(Guid subTaskId, Guid userId)
    {
        var subTask = await _subTaskRepository.GetByIdAsync(subTaskId);

        // BR-020, BR-029: Alt görev veya üst görev yoksa 404
        if (subTask is null || subTask.Task is null)
        {
            throw new NotFoundException("Alt görev bulunamadı.");
        }

        var isOwner = subTask.Task.OwnerId == userId;
        var isShared = subTask.Task.TaskShares != null && subTask.Task.TaskShares.Any(ts => ts.UserId == userId);

        if (!isOwner && !isShared)
        {
            throw new NotFoundException("Alt görev bulunamadı.");
        }

        // BR-018: Üst görev silinmişse alt göreve erişilemez
        if (subTask.Task.IsDeleted)
        {
            throw new NotFoundException("Alt görev bulunamadı.");
        }

        return subTask;
    }

    public async Task<SubTask> EnsureCanDeleteSubTaskAsync(Guid subTaskId, Guid userId)
    {
        var subTask = await _subTaskRepository.GetByIdAsync(subTaskId);

        if (subTask is null || subTask.Task is null)
        {
            throw new NotFoundException("Alt görev bulunamadı.");
        }

        // BR-020, BR-026, BR-029: YALNIZCA üst görevin sahibi silebilir! Paylaşılan kullanıcı silemez.
        if (subTask.Task.OwnerId != userId)
        {
            throw new NotFoundException("Alt görev bulunamadı.");
        }

        if (subTask.Task.IsDeleted)
        {
            throw new NotFoundException("Alt görev bulunamadı.");
        }

        return subTask;
    }
}

