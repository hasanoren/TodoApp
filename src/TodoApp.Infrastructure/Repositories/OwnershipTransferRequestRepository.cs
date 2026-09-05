using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Infrastructure.Data;

namespace TodoApp.Infrastructure.Repositories;

public class OwnershipTransferRequestRepository : IOwnershipTransferRequestRepository
{
    private readonly ApplicationDbContext _context;

    public OwnershipTransferRequestRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OwnershipTransferRequest?> GetByIdAsync(Guid id)
    {
        return await _context.OwnershipTransferRequests
            .Include(r => r.Task)
            .Include(r => r.FromUser)
            .Include(r => r.ToUser)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<OwnershipTransferRequest>> GetPendingByToUserIdAsync(Guid toUserId)
    {
        return await _context.OwnershipTransferRequests
            .Include(r => r.Task)
            .Include(r => r.FromUser)
            .Include(r => r.ToUser)
            .Where(r => r.ToUserId == toUserId && r.Status == TransferRequestStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<OwnershipTransferRequest?> GetActivePendingByTaskIdAsync(Guid taskId)
    {
        return await _context.OwnershipTransferRequests
            .FirstOrDefaultAsync(r => r.TaskId == taskId && r.Status == TransferRequestStatus.Pending);
    }

    public async Task AddAsync(OwnershipTransferRequest request)
    {
        await _context.OwnershipTransferRequests.AddAsync(request);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}

