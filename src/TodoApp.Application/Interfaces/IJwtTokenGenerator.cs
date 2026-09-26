using System.Security.Claims;
using TodoApp.Domain.Entities;

namespace TodoApp.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
    (string Token, DateTime ExpiresAt) GenerateRefreshToken();
    (string Token, DateTime ExpiresAt) GeneratePasswordResetToken();
    (string Token, DateTime ExpiresAt) GenerateTwoFactorTempToken(User user);
    ClaimsPrincipal? ValidateTwoFactorTempToken(string token);
}
