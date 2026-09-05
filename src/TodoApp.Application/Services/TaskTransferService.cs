using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class TaskTransferService : ITaskTransferService
{
    private readonly IOwnershipTransferRequestRepository _transferRequestRepo;
    private readonly ITodoItemRepository _todoItemRepo;
    private readonly IUserRepository _userRepo;
    private readonly ITaskShareRepository _taskShareRepo;

    public TaskTransferService(
        IOwnershipTransferRequestRepository transferRequestRepo,
        ITodoItemRepository todoItemRepo,
        IUserRepository userRepo,
        ITaskShareRepository taskShareRepo)
    {
        _transferRequestRepo = transferRequestRepo;
        _todoItemRepo = todoItemRepo;
        _userRepo = userRepo;
        _taskShareRepo = taskShareRepo;
    }

    public async Task<TransferRequestResponse> CreateTransferRequestAsync(
        Guid currentOwnerId, Guid taskId, CreateTransferRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NewOwnerEmail))
        {
            throw new ValidationException("Yeni sahip e-posta adresi boş olamaz.");
        }

        var task = await _todoItemRepo.GetByIdAsync(taskId);

        // BR-030 & BR-029: Sadece mevcut görev sahibi devir talebi oluşturabilir
        if (task is null || task.OwnerId != currentOwnerId)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        if (task.IsDeleted)
        {
            throw new ValidationException("Silinmiş bir görevin sahipliği devredilemez.");
        }

        var targetUser = await _userRepo.GetByEmailAsync(dto.NewOwnerEmail.Trim());
        if (targetUser is null)
        {
            throw new NotFoundException("Yeni sahip olarak belirtilen kullanıcı bulunamadı.");
        }

        if (targetUser.Id == currentOwnerId)
        {
            throw new ValidationException("Görevin sahipliğini zaten elinizde bulunduruyorsunuz.");
        }

        // Görev için zaten bekleyen aktif bir talep var mı kontrolü
        var existingPending = await _transferRequestRepo.GetActivePendingByTaskIdAsync(taskId);
        if (existingPending is not null)
        {
            throw new ConflictException("Bu görev için zaten bekleyen bir devir talebi bulunmaktadır.");
        }

        var transferRequest = new OwnershipTransferRequest
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            FromUserId = currentOwnerId,
            ToUserId = targetUser.Id,
            Status = TransferRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _transferRequestRepo.AddAsync(transferRequest);
        await _transferRequestRepo.SaveChangesAsync();

        var currentUser = await _userRepo.GetByIdAsync(currentOwnerId);

        return new TransferRequestResponse
        {
            Id = transferRequest.Id,
            TaskId = taskId,
            TaskTitle = task.Title,
            FromUserId = currentOwnerId,
            FromUserEmail = currentUser?.Email ?? string.Empty,
            ToUserId = targetUser.Id,
            ToUserEmail = targetUser.Email,
            Status = transferRequest.Status.ToString(),
            CreatedAt = transferRequest.CreatedAt
        };
    }

    public async Task<List<TransferRequestResponse>> GetPendingRequestsAsync(Guid userId)
    {
        var requests = await _transferRequestRepo.GetPendingByToUserIdAsync(userId);

        return requests.Select(r => new TransferRequestResponse
        {
            Id = r.Id,
            TaskId = r.TaskId,
            TaskTitle = r.Task?.Title ?? string.Empty,
            FromUserId = r.FromUserId,
            FromUserEmail = r.FromUser?.Email ?? string.Empty,
            ToUserId = r.ToUserId,
            ToUserEmail = r.ToUser?.Email ?? string.Empty,
            Status = r.Status.ToString(),
            CreatedAt = r.CreatedAt,
            RespondedAt = r.RespondedAt
        }).ToList();
    }

    public async Task AcceptTransferRequestAsync(Guid targetUserId, Guid requestId)
    {
        var request = await _transferRequestRepo.GetByIdAsync(requestId);

        if (request is null || request.ToUserId != targetUserId)
        {
            throw new NotFoundException("Devir talebi bulunamadı.");
        }

        if (request.Status != TransferRequestStatus.Pending)
        {
            throw new ValidationException("Bu devir talebi zaten yanıtlanmış veya iptal edilmiş.");
        }

        var task = await _todoItemRepo.GetByIdAsync(request.TaskId);
        if (task is null || task.IsDeleted)
        {
            throw new ValidationException("İlgili görev artık geçerli değil.");
        }

        var oldOwnerId = task.OwnerId;

        // Yeni sahip önceden paylaşılanlar arasındaysa çıkar
        var newOwnerShare = await _taskShareRepo.GetAsync(task.Id, targetUserId);
        if (newOwnerShare is not null)
        {
            _taskShareRepo.Remove(newOwnerShare);
        }

        // Sahipliği yeni kullanıcıya geçir
        task.OwnerId = targetUserId;

        // Eski sahibi paylaşılan kullanıcı yap
        var oldOwnerShare = new TaskShare
        {
            TaskId = task.Id,
            UserId = oldOwnerId,
            SharedAt = DateTime.UtcNow
        };
        await _taskShareRepo.AddAsync(oldOwnerShare);

        // Talebi Accepted olarak güncelle
        request.Status = TransferRequestStatus.Accepted;
        request.RespondedAt = DateTime.UtcNow;

        await _taskShareRepo.SaveChangesAsync();
        await _todoItemRepo.SaveChangesAsync();
        await _transferRequestRepo.SaveChangesAsync();
    }

    public async Task RejectTransferRequestAsync(Guid targetUserId, Guid requestId)
    {
        var request = await _transferRequestRepo.GetByIdAsync(requestId);

        if (request is null || request.ToUserId != targetUserId)
        {
            throw new NotFoundException("Devir talebi bulunamadı.");
        }

        if (request.Status != TransferRequestStatus.Pending)
        {
            throw new ValidationException("Bu devir talebi zaten yanıtlanmış veya iptal edilmiş.");
        }

        request.Status = TransferRequestStatus.Rejected;
        request.RespondedAt = DateTime.UtcNow;

        await _transferRequestRepo.SaveChangesAsync();
    }

    public async Task CancelTransferRequestAsync(Guid ownerUserId, Guid requestId)
    {
        var request = await _transferRequestRepo.GetByIdAsync(requestId);

        if (request is null || request.FromUserId != ownerUserId)
        {
            throw new NotFoundException("Devir talebi bulunamadı.");
        }

        if (request.Status != TransferRequestStatus.Pending)
        {
            throw new ValidationException("Bu devir talebi zaten yanıtlanmış veya iptal edilmiş.");
        }

        request.Status = TransferRequestStatus.Cancelled;
        request.RespondedAt = DateTime.UtcNow;

        await _transferRequestRepo.SaveChangesAsync();
    }
}

