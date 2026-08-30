using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailSender emailSender,
        IPasswordResetTokenRepository passwordResetTokenRepository)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailSender = emailSender;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        // _configuration kaldırıldı — artık hiç kullanılmıyor
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userRepository.GetByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            throw new ConflictException("Bu e-posta adresi zaten kayıtlı.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new ValidationException("E-posta veya şifre hatalı.");
        }

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var storedToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

        if (storedToken is null || !storedToken.IsActive)
        {
            throw new ValidationException("Geçersiz veya süresi dolmuş refresh token.");
        }

        storedToken.IsRevoked = true;

        return await GenerateAuthResponseAsync(storedToken.User);
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user)
    {
        var accessToken = _jwtTokenGenerator.GenerateToken(user);
        var (refreshTokenValue, expiresAt) = _jwtTokenGenerator.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = expiresAt,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.AddAsync(refreshToken);
        await _refreshTokenRepository.SaveChangesAsync();

        return new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            Token = accessToken,
            RefreshToken = refreshTokenValue
        };
    }

    public async Task LogoutAsync(RefreshTokenRequest request)
    {
        var storedToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

        if (storedToken is null)
        {
            // Token zaten yoksa veya daha önce silinmişse, sessizce başarı say
            // (saldırgana "bu token var mı yok mu" bilgisini sızdırmamak için)
            return;
        }

        storedToken.IsRevoked = true;
        await _refreshTokenRepository.SaveChangesAsync();
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);

        if (user is null)
        {
            return; // BR (T1.4.5): kullanıcı yoksa sessizce çık, email gönderme, ama Controller yine de aynı mesajı dönecek
        }

        var (token, expiresAt) = _jwtTokenGenerator.GeneratePasswordResetToken();

        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = token,
            ExpiresAt = expiresAt,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        await _passwordResetTokenRepository.AddAsync(resetToken);
        await _passwordResetTokenRepository.SaveChangesAsync();

        var resetLink =
     $"http://localhost:5240/api/Auth/reset-password?token={Uri.EscapeDataString(token)}";

        var htmlBody = $"""
    <p>Merhaba,</p>
    <p>Şifreni sıfırlamak için aşağıdaki linke tıkla:</p>
    <p>
        <a href="{resetLink}">Şifremi Sıfırla</a>
    </p>
    <p>Bu link 60 dakika geçerlidir.</p>
    """;

        await _emailSender.SendEmailAsync(
            user.Email,
            "TodoApp - Şifre Sıfırlama",
            htmlBody);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var storedToken = await _passwordResetTokenRepository.GetByTokenAsync(request.Token);

        if (storedToken is null || !storedToken.IsActive)
        {
            throw new ValidationException("Geçersiz veya süresi dolmuş sıfırlama bağlantısı.");
        }

        storedToken.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        storedToken.IsUsed = true;

        await _passwordResetTokenRepository.SaveChangesAsync();
    }

    public async Task ChangePasswordAsync(
     Guid userId,
     ChangePasswordRequest request)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user is null)
        {
            throw new ValidationException("Kullanıcı bulunamadı.");
        }

        var isPasswordCorrect = BCrypt.Net.BCrypt.Verify(
            request.CurrentPassword,
            user.PasswordHash);

        if (!isPasswordCorrect)
        {
            throw new ValidationException("Mevcut şifre hatalı.");
        }

        user.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        // Kullanıcının tüm refresh tokenlarını geçersiz hale getir
        foreach (var refreshToken in user.RefreshTokens)
        {
            refreshToken.IsRevoked = true;
        }

        await _userRepository.SaveChangesAsync();
    }
}