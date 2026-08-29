using TodoApp.Domain.Entities;

namespace TodoApp.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
    (string Token, DateTime ExpiresAt) GenerateRefreshToken();   // YENİ
}