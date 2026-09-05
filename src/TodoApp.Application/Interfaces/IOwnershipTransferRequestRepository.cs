using TodoApp.Domain.Entities;

namespace TodoApp.Application.Interfaces;

public interface IOwnershipTransferRequestRepository
{
    Task<OwnershipTransferRequest?> GetByIdAsync(Guid id);
    Task<List<OwnershipTransferRequest>> GetPendingByToUserIdAsync(Guid toUserId);
    Task<OwnershipTransferRequest?> GetActivePendingByTaskIdAsync(Guid taskId);
    Task AddAsync(OwnershipTransferRequest request);
    Task SaveChangesAsync();
}

