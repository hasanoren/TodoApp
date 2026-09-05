using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class TaskShareService : ITaskShareService
{
    private readonly ITaskShareRepository _taskShareRepository;
    private readonly ITodoItemRepository _todoItemRepository;
    private readonly IUserRepository _userRepository;

    public TaskShareService(
        ITaskShareRepository taskShareRepository,
        ITodoItemRepository todoItemRepository,
        IUserRepository userRepository)
    {
        _taskShareRepository = taskShareRepository;
        _todoItemRepository = todoItemRepository;
        _userRepository = userRepository;
    }

    public async Task ShareAsync(Guid ownerUserId, Guid taskId, ShareTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ValidationException("E-posta adresi boş olamaz.");
        }

        var task = await _todoItemRepository.GetByIdAsync(taskId);

        // BR-013 & BR-029: Sadece görev sahibi paylaşım yapabilir, yetkisizse 404
        if (task is null || task.OwnerId != ownerUserId)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        if (task.IsDeleted)
        {
            throw new ValidationException("Silinmiş bir görev paylaşılamaz.");
        }

        var targetUser = await _userRepository.GetByEmailAsync(request.Email.Trim());

        // BR-027: Var olmayan bir kullanıcıyla paylaşım yapılırsa hata döner
        if (targetUser is null)
        {
            throw new NotFoundException("Paylaşılmak istenen kullanıcı bulunamadı.");
        }

        // BR-004: Kullanıcı kendi görevini kendisiyle paylaşamaz
        if (targetUser.Id == ownerUserId)
        {
            throw new ValidationException("Kullanıcı görevi kendisiyle paylaşamaz.");
        }

        // BR-014: Zaten paylaşılmışsa sessizce yok sayılır (idempotent)
        var existingShare = await _taskShareRepository.GetAsync(taskId, targetUser.Id);
        if (existingShare is not null)
        {
            return;
        }

        var share = new TaskShare
        {
            TaskId = taskId,
            UserId = targetUser.Id,
            SharedAt = DateTime.UtcNow
        };

        await _taskShareRepository.AddAsync(share);
        await _taskShareRepository.SaveChangesAsync();
    }

    public async Task<List<SharedUserResponse>> GetSharedUsersAsync(Guid userId, Guid taskId)
    {
        var task = await _todoItemRepository.GetByIdAsync(taskId);

        if (task is null || task.IsDeleted)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        // BR-029: Sadece owner veya görevin paylaşıldığı kişiler listeyi görebilir
        var isShared = await _taskShareRepository.IsSharedWithUserAsync(taskId, userId);
        if (task.OwnerId != userId && !isShared)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        var shares = await _taskShareRepository.GetByTaskIdAsync(taskId);

        return shares.Select(s => new SharedUserResponse
        {
            UserId = s.UserId,
            Email = s.User?.Email ?? string.Empty,
            SharedAt = s.SharedAt
        }).ToList();
    }

    public async Task RemoveShareAsync(Guid ownerUserId, Guid taskId, Guid targetUserId)
    {
        var task = await _todoItemRepository.GetByIdAsync(taskId);

        // BR-013 & BR-029: Sadece görev sahibi birinin yetkisini kaldırabilir
        if (task is null || task.OwnerId != ownerUserId)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        var share = await _taskShareRepository.GetAsync(taskId, targetUserId);
        if (share is null)
        {
            throw new NotFoundException("Paylaşım kaydı bulunamadı.");
        }

        _taskShareRepository.Remove(share);
        await _taskShareRepository.SaveChangesAsync();
    }

    public async Task LeaveShareAsync(Guid sharedUserId, Guid taskId)
    {
        var task = await _todoItemRepository.GetByIdAsync(taskId);
        if (task is null)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        // BR-028: Paylaşılan kullanıcı kendi isteğiyle paylaşımdan çıkabilir
        var share = await _taskShareRepository.GetAsync(taskId, sharedUserId);
        if (share is null)
        {
            throw new NotFoundException("Bu görev sizinle paylaşılmamış.");
        }

        _taskShareRepository.Remove(share);
        await _taskShareRepository.SaveChangesAsync();
    }
}
