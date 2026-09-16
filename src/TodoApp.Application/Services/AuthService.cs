using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TodoApp.Application.Common;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Settings;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly PasswordResetSettings _passwordResetSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailSender emailSender,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IPasswordHasher passwordHasher,
        IOptions<PasswordResetSettings> passwordResetOptions,
        ILogger<AuthService>? logger = null)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailSender = emailSender;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _passwordHasher = passwordHasher;
        _passwordResetSettings = passwordResetOptions.Value;
        _logger = logger ?? NullLogger<AuthService>.Instance;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail);
        if (existingUser is not null)
        {
            throw new ConflictException("Bu e-posta adresi zaten kayıtlı.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("Yeni kullanıcı kaydı oluşturuldu. UserId: {UserId}, Email: {Email}", user.Id, user.Email);

        return await GenerateAuthResponseAsync(user);
    }

    // Zamanlama saldırılarını (timing attack / account enumeration) engellemek için sahte BCrypt hash'i
    private const string DummyHash = "$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);

        // Kullanıcı bulunamasa dahi sahte hash ile doğrulama çalıştırılarak süre eşitlenir
        var passwordHash = user?.PasswordHash ?? DummyHash;
        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, passwordHash);

        if (user is null || !isPasswordValid)
        {
            _logger.LogWarning("Başarısız giriş denemesi. Email: {Email}", request.Email);
            throw new ValidationException("E-posta veya şifre hatalı.");
        }

        _logger.LogInformation("Kullanıcı başarıyla giriş yaptı. UserId: {UserId}, Email: {Email}", user.Id, user.Email);

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var tokenHash = TokenHelper.HashToken(request.RefreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenAsync(tokenHash);

        if (storedToken is null || !storedToken.IsActive)
        {
            _logger.LogWarning("Geçersiz veya süresi dolmuş refresh token denemesi.");
            throw new ValidationException("Geçersiz veya süresi dolmuş refresh token.");
        }

        storedToken.IsRevoked = true;
        _logger.LogInformation("Refresh token rotasyonu gerçekleştirildi. UserId: {UserId}", storedToken.UserId);

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
            Token = TokenHelper.HashToken(refreshTokenValue), // Güvenlik (T8.1.7): DB'de SHA-256 hash olarak sakla
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
        var tokenHash = TokenHelper.HashToken(request.RefreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenAsync(tokenHash);

        if (storedToken is null)
        {
            // Token zaten yoksa veya daha önce silinmişse, sessizce başarı say
            // (saldırgana "bu token var mı yok mu" bilgisini sızdırmamak için)
            return;
        }

        storedToken.IsRevoked = true;
        await _refreshTokenRepository.SaveChangesAsync();
        _logger.LogInformation("Kullanıcı oturumu sonlandırıldı (Logout). UserId: {UserId}", storedToken.UserId);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);

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
            $"{_passwordResetSettings.ResetUrl}?token={Uri.EscapeDataString(token)}";

        var htmlBody = $"""
    <p>Merhaba,</p>
    <p>Şifreni sıfırlamak için aşağıdaki linke tıkla:</p>
    <p>
        <a href="{resetLink}">Şifremi Sıfırla</a>
    </p>
    <p>Bu link {_passwordResetSettings.ExpiryMinutes} dakika geçerlidir.</p>
    """;

        await _emailSender.SendEmailAsync(
            user.Email,
            "TodoApp - Şifre Sıfırlama",
            htmlBody);

        _logger.LogInformation("Şifre sıfırlama e-postası gönderildi. Email: {Email}", user.Email);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var storedToken = await _passwordResetTokenRepository.GetByTokenAsync(request.Token);

        if (storedToken is null || !storedToken.IsActive)
        {
            _logger.LogWarning("Geçersiz veya süresi dolmuş şifre sıfırlama bağlantısı denemesi.");
            throw new ValidationException("Geçersiz veya süresi dolmuş sıfırlama bağlantısı.");
        }

        storedToken.User.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        storedToken.IsUsed = true;

        // Güvenlik (T8.1.6): Şifre sıfırlandığında tüm açık oturumları geçersiz kıl
        foreach (var refreshToken in storedToken.User.RefreshTokens)
        {
            refreshToken.IsRevoked = true;
        }

        await _passwordResetTokenRepository.SaveChangesAsync();
        _logger.LogInformation("Kullanıcı şifresi sıfırlandı ve tüm oturumları geçersiz kılındı. UserId: {UserId}", storedToken.UserId);
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

        var isPasswordCorrect = _passwordHasher.VerifyPassword(
            request.CurrentPassword,
            user.PasswordHash);

        if (!isPasswordCorrect)
        {
            _logger.LogWarning("Şifre değiştirme başarısız: Mevcut şifre hatalı. UserId: {UserId}", userId);
            throw new ValidationException("Mevcut şifre hatalı.");
        }

        user.PasswordHash =
            _passwordHasher.HashPassword(request.NewPassword);

        // Kullanıcının tüm refresh tokenlarını geçersiz hale getir
        foreach (var refreshToken in user.RefreshTokens)
        {
            refreshToken.IsRevoked = true;
        }

        await _userRepository.SaveChangesAsync();
        _logger.LogInformation("Kullanıcı şifresini değiştirdi ve tüm oturumları geçersiz kılındı. UserId: {UserId}", userId);
    }
}