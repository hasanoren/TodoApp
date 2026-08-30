using TodoApp.Domain.Entities;

namespace TodoApp.Application.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token);
    Task<PasswordResetToken?> GetByTokenAsync(string token);
    Task SaveChangesAsync();
}