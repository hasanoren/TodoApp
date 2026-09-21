using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TodoApp.Application.Common;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Settings;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;
using OtpNet;

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

        // Hesap kilitli mi kontrolü (T10.2.1)
        if (user != null && user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            var remainingMinutes = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
            _logger.LogWarning("Kilitli hesaba giriş denemesi. Email: {Email}, Kalan Süre: {Minutes} dk", request.Email, remainingMinutes);
            throw new ValidationException($"Çok fazla hatalı giriş denemesi yapıldı. Hesabınız geçici olarak kilitlendi. Lütfen {remainingMinutes} dakika sonra tekrar deneyin.");
        }

        // Kullanıcı bulunamasa dahi sahte hash ile doğrulama çalıştırılarak süre eşitlenir
        var passwordHash = user?.PasswordHash ?? DummyHash;
        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, passwordHash);

        if (user is null || !isPasswordValid)
        {
            if (user is not null)
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                    _logger.LogWarning("Kullanıcı hesabı 5 hatalı deneme nedeniyle 15 dakika kilitlendi. UserId: {UserId}", user.Id);
                }
                await _userRepository.SaveChangesAsync();
            }

            _logger.LogWarning("Başarısız giriş denemesi. Email: {Email}", request.Email);
            throw new ValidationException("E-posta veya şifre hatalı.");
        }

        // Başarılı giriş: Sayaç ve kilidi sıfırla
        if (user.FailedLoginAttempts > 0 || user.LockoutEnd.HasValue)
        {
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            await _userRepository.SaveChangesAsync();
        }

        if (user.TwoFactorEnabled)
        {
            _logger.LogInformation("2FA gerekli. UserId: {UserId}", user.Id);
            return new AuthResponse
            {
                RequiresTwoFactor = true,
                UserId = user.Id
            };
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

        try
        {
            await _emailSender.SendEmailAsync(
                user.Email,
                "TodoApp - Şifre Sıfırlama",
                htmlBody);

            _logger.LogInformation("Şifre sıfırlama e-postası gönderildi. Email: {Email}", user.Email);
        }
        catch (Exception ex)
        {
            // Güvenlik (T10.2.3): SMTP hatası 500 fırlatarak kullanıcı tespiti (enumeration) yapılmasına izin vermesin
            _logger.LogError(ex, "Şifre sıfırlama e-postası gönderilirken hata oluştu. Email: {Email}", user.Email);
        }
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
        storedToken.User.SecurityStamp = Guid.NewGuid(); // T10.2.2: Mevcut JWT'leri iptal et
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
        user.SecurityStamp = Guid.NewGuid(); // T10.2.2: Mevcut JWT'leri iptal et

        // Kullanıcının tüm refresh tokenlarını geçersiz hale getir
        foreach (var refreshToken in user.RefreshTokens)
        {
            refreshToken.IsRevoked = true;
        }

        await _userRepository.SaveChangesAsync();
        _logger.LogInformation("Kullanıcı şifresini değiştirdi ve tüm oturumları geçersiz kılındı. UserId: {UserId}", userId);
    }

    public async Task<TwoFactorEnableResponse> EnableTwoFactorAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null) throw new ValidationException("Kullanıcı bulunamadı.");

        var key = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(key);

        user.TwoFactorSecret = base32Secret;
        user.TwoFactorEnabled = false;

        await _userRepository.SaveChangesAsync();

        var issuer = "TodoApp";
        var accountTitle = Uri.EscapeDataString(user.Email);
        var qrCodeUri = $"otpauth://totp/{issuer}:{accountTitle}?secret={base32Secret}&issuer={issuer}";

        return new TwoFactorEnableResponse
        {
            Secret = base32Secret,
            QrCodeUri = qrCodeUri
        };
    }

    public async Task VerifyTwoFactorSetupAsync(Guid userId, TwoFactorVerifyRequest request)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null || string.IsNullOrEmpty(user.TwoFactorSecret))
            throw new ValidationException("Geçersiz istek.");

        var base32Bytes = Base32Encoding.ToBytes(user.TwoFactorSecret);
        var totp = new Totp(base32Bytes);

        if (!totp.VerifyTotp(request.Code, out long timeStepMatched, window: new VerificationWindow(previous: 1, future: 1)))
        {
            throw new ValidationException("Geçersiz veya süresi dolmuş kod.");
        }

        user.TwoFactorEnabled = true;
        user.SecurityStamp = Guid.NewGuid(); // T10.2.2: Mevcut JWT'leri iptal et
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("2FA aktifleştirildi. UserId: {UserId}", userId);
    }

    public async Task<AuthResponse> LoginWithTwoFactorAsync(TwoFactorLoginRequest request)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user is null || !user.TwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            throw new ValidationException("Geçersiz istek.");
        }

        var base32Bytes = Base32Encoding.ToBytes(user.TwoFactorSecret);
        var totp = new Totp(base32Bytes);

        if (!totp.VerifyTotp(request.Code, out long timeStepMatched, window: new VerificationWindow(previous: 1, future: 1)))
        {
            throw new ValidationException("Geçersiz veya süresi dolmuş kod.");
        }

        _logger.LogInformation("2FA girişi başarılı. UserId: {UserId}", user.Id);
        return await GenerateAuthResponseAsync(user);
    }

    public async Task DisableTwoFactorAsync(Guid userId, TwoFactorVerifyRequest request)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) throw new NotFoundException("Kullanıcı bulunamadı.");

        if (!user.TwoFactorEnabled)
        {
            throw new ValidationException("İki adımlı doğrulama zaten devre dışı.");
        }

        var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));
        if (!totp.VerifyTotp(request.Code, out _, VerificationWindow.RfcSpecifiedNetworkDelay))
        {
            throw new ValidationException("Geçersiz doğrulama kodu.");
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = string.Empty;
        user.SecurityStamp = Guid.NewGuid(); // T10.2.2: Mevcut JWT'leri iptal et

        await _userRepository.SaveChangesAsync();
        _logger.LogInformation("2FA devre dışı bırakıldı. UserId: {UserId}", userId);
    }

    public async Task DeleteAccountAsync(Guid userId, string password)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) throw new NotFoundException("Kullanıcı bulunamadı.");

        // T10.2.5: Hesap silme gibi kritik bir işlem öncesi parola doğrulaması (Re-authentication) zorunludur
        var isPasswordValid = _passwordHasher.VerifyPassword(password, user.PasswordHash);
        if (!isPasswordValid)
        {
            _logger.LogWarning("Hesap silme başarısız: Hatalı parola. UserId: {UserId}", userId);
            throw new ValidationException("Şifre hatalı.");
        }

        await _userRepository.DeleteAsync(user);
        _logger.LogInformation("Kullanıcı hesabı başarıyla silindi. UserId: {UserId}", userId);
    }
}