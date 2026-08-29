using TodoApp.Domain.Entities;

namespace TodoApp.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken refreshToken);
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task SaveChangesAsync();
}