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

    public async Task DeleteAsync(User user)
    {
        // T10.3.1: Tüm silme adımları tek bir veritabanı transaction'ında atomik yürütülür
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // SQL Server multiple cascade paths nedeniyle NoAction olan ilişkileri manuel temizliyoruz
            // Not: Soft-delete edilmiş (IsDeleted = true) kayıtların da temizlenebilmesi için IgnoreQueryFilters() şarttır.
            await _context.TodoItems.IgnoreQueryFilters()
                .Where(t => t.CompletedByUserId == user.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.CompletedByUserId, (Guid?)null));

            await _context.TodoItems.IgnoreQueryFilters()
                .Where(t => t.DeletedByUserId == user.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.DeletedByUserId, (Guid?)null));

            await _context.TaskShares.Where(ts => ts.UserId == user.Id).ExecuteDeleteAsync();

            await _context.OwnershipTransferRequests.Where(otr => otr.FromUserId == user.Id || otr.ToUserId == user.Id).ExecuteDeleteAsync();

            await _context.TodoItemActivities.Where(a => a.UserId == user.Id).ExecuteDeleteAsync();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id);
    }
}