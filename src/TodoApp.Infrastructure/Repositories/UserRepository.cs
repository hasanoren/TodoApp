using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Infrastructure.Data;

namespace TodoApp.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public void Delete(User user)
    {
        // SQL Server multiple cascade paths nedeniyle NoAction olan ilişkileri manuel temizliyoruz
        _context.TodoItems.Where(t => t.CompletedByUserId == user.Id)
            .ExecuteUpdate(s => s.SetProperty(t => t.CompletedByUserId, (Guid?)null));

        _context.TodoItems.Where(t => t.DeletedByUserId == user.Id)
            .ExecuteUpdate(s => s.SetProperty(t => t.DeletedByUserId, (Guid?)null));

        _context.TaskShares.Where(ts => ts.UserId == user.Id).ExecuteDelete();

        _context.OwnershipTransferRequests.Where(otr => otr.FromUserId == user.Id || otr.ToUserId == user.Id).ExecuteDelete();

        _context.Users.Remove(user);
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id);
    }
}