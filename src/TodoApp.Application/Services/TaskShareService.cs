using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class TaskShareService : ITaskShareService
{
    private readonly ITaskShareRepository _taskShareRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITaskAuthorizationService _taskAuthorizationService;

    public TaskShareService(
        ITaskShareRepository taskShareRepository,
        IUserRepository userRepository,
        ITaskAuthorizationService taskAuthorizationService)
    {
        _taskShareRepository = taskShareRepository;
        _userRepository = userRepository;
        _taskAuthorizationService = taskAuthorizationService;
    }

    public async Task ShareAsync(Guid ownerUserId, Guid taskId, ShareTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ValidationException("E-posta adresi boş olamaz.");
        }

        // BR-013 & BR-029: Sadece görev sahibi paylaşım yapabilir, yetkisizse 404
        var task = await _taskAuthorizationService.EnsureOwnerAsync(taskId, ownerUserId);

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
        // BR-029: Sadece owner veya görevin paylaşıldığı kişiler listeyi görebilir (silinmemişse)
        await _taskAuthorizationService.EnsureCanReadAsync(taskId, userId);

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
        // BR-013 & BR-029: Sadece görev sahibi birinin yetkisini kaldırabilir
        await _taskAuthorizationService.EnsureOwnerAsync(taskId, ownerUserId);

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
