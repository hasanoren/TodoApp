using TodoApp.Application.DTOs;

namespace TodoApp.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task LogoutAsync(RefreshTokenRequest request);
    Task ForgotPasswordAsync(ForgotPasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<TwoFactorEnableResponse> EnableTwoFactorAsync(Guid userId);
    Task VerifyTwoFactorSetupAsync(Guid userId, TwoFactorVerifyRequest request);
    Task<AuthResponse> LoginWithTwoFactorAsync(TwoFactorLoginRequest request);
    Task DisableTwoFactorAsync(Guid userId, TwoFactorVerifyRequest request);
    Task DeleteAccountAsync(Guid userId);
}
