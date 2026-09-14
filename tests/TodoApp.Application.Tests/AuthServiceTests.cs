using Microsoft.Extensions.Options;
using Moq;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Services;
using TodoApp.Application.Settings;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;
using Xunit;

namespace TodoApp.Application.Tests;

public class AuthServiceTests
{
    private static IOptions<PasswordResetSettings> DefaultPasswordResetOptions =>
        Options.Create(new PasswordResetSettings
        {
            ExpiryMinutes = 60,
            ResetUrl = "http://localhost:5240/api/Auth/reset-password"
        });

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ThrowsConflictException()
    {
        // ARRANGE
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashli-sifre",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository
            .Setup(repo => repo.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(existingUser);

        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object,
            mockPasswordHasher.Object,
            DefaultPasswordResetOptions);

        var registerRequest = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "YeniSifre123!"
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ConflictException>(
            () => authService.RegisterAsync(registerRequest));
    }

    [Fact]
    public async Task RegisterAsync_WhenValidRequest_HashesPasswordAndReturnsAuthResponse()
    {
        // ARRANGE
        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository
            .Setup(repo => repo.GetByEmailAsync("new@example.com"))
            .ReturnsAsync((User?)null);

        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtTokenGenerator
            .Setup(gen => gen.GenerateToken(It.IsAny<User>()))
            .Returns("access-token-123");
        mockJwtTokenGenerator
            .Setup(gen => gen.GenerateRefreshToken())
            .Returns(("refresh-token-123", DateTime.UtcNow.AddDays(7)));

        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();
        mockPasswordHasher
            .Setup(hasher => hasher.HashPassword("PlainSecret123!"))
            .Returns("hashed-secret-value");

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object,
            mockPasswordHasher.Object,
            DefaultPasswordResetOptions);

        var request = new RegisterRequest
        {
            Email = "new@example.com",
            Password = "PlainSecret123!"
        };

        // ACT
        var response = await authService.RegisterAsync(request);

        // ASSERT
        Assert.NotNull(response);
        Assert.Equal("new@example.com", response.Email);
        Assert.Equal("access-token-123", response.Token);
        Assert.Equal("refresh-token-123", response.RefreshToken);
        mockUserRepository.Verify(repo => repo.AddAsync(It.Is<User>(u => u.PasswordHash == "hashed-secret-value")), Times.Once);
        mockUserRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsIncorrect_ThrowsValidationException()
    {
        // ARRANGE
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashli-sifre",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository
            .Setup(repo => repo.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(existingUser);

        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();
        mockPasswordHasher
            .Setup(hasher => hasher.VerifyPassword("YanlisSifre!", "hashli-sifre"))
            .Returns(false);

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object,
            mockPasswordHasher.Object,
            DefaultPasswordResetOptions);

        var loginRequest = new LoginRequest
        {
            Email = "test@example.com",
            Password = "YanlisSifre!"
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ValidationException>(
            () => authService.LoginAsync(loginRequest));
    }

    [Fact]
    public async Task LoginAsync_WhenUserNotFound_ExecutesDummyPasswordVerificationToPreventTimingAttackAndThrowsValidationException()
    {
        // ARRANGE: User does NOT exist in DB
        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository
            .Setup(repo => repo.GetByEmailAsync("notfound@example.com"))
            .ReturnsAsync((User?)null);

        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object,
            mockPasswordHasher.Object,
            DefaultPasswordResetOptions);

        var loginRequest = new LoginRequest
        {
            Email = "notfound@example.com",
            Password = "SomePassword123!"
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ValidationException>(
            () => authService.LoginAsync(loginRequest));

        // ASSERT: Zamanlama saldırısını önlemek için DummyHash ile doğrulamanın MUTLAKA çağrıldığını teyit et
        mockPasswordHasher.Verify(
            hasher => hasher.VerifyPassword("SomePassword123!", It.Is<string>(h => h.StartsWith("$2a$11$"))),
            Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsCorrect_ReturnsAuthResponse()
    {
        // ARRANGE
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashli-sifre",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository
            .Setup(repo => repo.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(existingUser);

        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtTokenGenerator
            .Setup(gen => gen.GenerateToken(existingUser))
            .Returns("login-jwt-token");
        mockJwtTokenGenerator
            .Setup(gen => gen.GenerateRefreshToken())
            .Returns(("login-refresh-token", DateTime.UtcNow.AddDays(7)));

        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();
        mockPasswordHasher
            .Setup(hasher => hasher.VerifyPassword("DogruSifre123!", "hashli-sifre"))
            .Returns(true);

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object,
            mockPasswordHasher.Object,
            DefaultPasswordResetOptions);

        var loginRequest = new LoginRequest
        {
            Email = "test@example.com",
            Password = "DogruSifre123!"
        };

        // ACT
        var response = await authService.LoginAsync(loginRequest);

        // ASSERT
        Assert.NotNull(response);
        Assert.Equal("login-jwt-token", response.Token);
        Assert.Equal("login-refresh-token", response.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenIsExpired_ThrowsValidationException()
    {
        // ARRANGE
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashli-sifre",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var expiredToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Token = "eski-bir-token-degeri",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow.AddDays(-8)
        };

        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        mockRefreshTokenRepository
            .Setup(repo => repo.GetByTokenAsync("eski-bir-token-degeri"))
            .ReturnsAsync(expiredToken);

        var mockUserRepository = new Mock<IUserRepository>();
        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object,
            mockPasswordHasher.Object,
            DefaultPasswordResetOptions);

        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = "eski-bir-token-degeri"
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ValidationException>(
            () => authService.RefreshTokenAsync(refreshRequest));
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenIsValid_RevokesOldTokenAndReturnsNewTokens()
    {
        // ARRANGE
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashli-sifre",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var validToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Token = "gecerli-token",
            ExpiresAt = DateTime.UtcNow.AddDays(5),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        mockRefreshTokenRepository
            .Setup(repo => repo.GetByTokenAsync("gecerli-token"))
            .ReturnsAsync(validToken);

        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtTokenGenerator
            .Setup(gen => gen.GenerateToken(It.IsAny<User>()))
            .Returns("yeni-access-token");
        mockJwtTokenGenerator
            .Setup(gen => gen.GenerateRefreshToken())
            .Returns(("yeni-refresh-token", DateTime.UtcNow.AddDays(7)));

        var mockUserRepository = new Mock<IUserRepository>();
        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object,
            mockPasswordHasher.Object,
            DefaultPasswordResetOptions);

        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = "gecerli-token"
        };

        // ACT
        var result = await authService.RefreshTokenAsync(refreshRequest);

        // ASSERT
        Assert.Equal("yeni-access-token", result.Token);
        Assert.Equal("yeni-refresh-token", result.RefreshToken);
        Assert.True(validToken.IsRevoked);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserNotFound_SilentlyReturnsWithoutSendingEmail()
    {
        // ARRANGE
        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository
            .Setup(repo => repo.GetByEmailAsync("nonexistent@example.com"))
            .ReturnsAsync((User?)null);

        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object,
            mockPasswordHasher.Object,
            DefaultPasswordResetOptions);

        var request = new ForgotPasswordRequest { Email = "nonexistent@example.com" };

        // ACT
        await authService.ForgotPasswordAsync(request);

        // ASSERT
        mockEmailSender.Verify(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        mockPasswordResetTokenRepository.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserFound_SendsEmailWithConfiguredResetUrl()
    {
        // ARRANGE
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var customOptions = Options.Create(new PasswordResetSettings
        {
            ExpiryMinutes = 30,
            ResetUrl = "https://myapp.com/auth/reset"
        });

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(repo => repo.GetByEmailAsync("user@example.com")).ReturnsAsync(user);

        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtTokenGenerator.Setup(g => g.GeneratePasswordResetToken()).Returns(("secret-token-xyz", DateTime.UtcNow.AddMinutes(30)));

        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object,
            mockPasswordHasher.Object,
            customOptions);

        var request = new ForgotPasswordRequest { Email = "user@example.com" };

        // ACT
        await authService.ForgotPasswordAsync(request);

        // ASSERT
        mockPasswordResetTokenRepository.Verify(r => r.AddAsync(It.Is<PasswordResetToken>(t => t.Token == "secret-token-xyz")), Times.Once);
        mockPasswordResetTokenRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        mockEmailSender.Verify(s => s.SendEmailAsync(
            "user@example.com",
            "TodoApp - Şifre Sıfırlama",
            It.Is<string>(body => body.Contains("https://myapp.com/auth/reset?token=secret-token-xyz") && body.Contains("30 dakika"))),
            Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenIsExpired_ThrowsValidationException()
    {
        // ARRANGE
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashli-sifre",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var expiredResetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Token = "suresi-dolmus-reset-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        mockPasswordResetTokenRepository
            .Setup(repo => repo.GetByTokenAsync("suresi-dolmus-reset-token"))
            .ReturnsAsync(expiredResetToken);

        var mockUserRepository = new Mock<IUserRepository>();
        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object,
            mockPasswordHasher.Object,
            DefaultPasswordResetOptions);

        var resetRequest = new ResetPasswordRequest
        {
            Token = "suresi-dolmus-reset-token",
            NewPassword = "YeniSifre123!"
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ValidationException>(
            () => authService.ResetPasswordAsync(resetRequest));
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenIsAlreadyUsed_ThrowsValidationException()
    {
        // ARRANGE
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashli-sifre",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var usedResetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Token = "kullanilmis-reset-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            IsUsed = true,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        mockPasswordResetTokenRepository
            .Setup(repo => repo.GetByTokenAsync("kullanilmis-reset-token"))
            .ReturnsAsync(usedResetToken);

        var mockUserRepository = new Mock<IUserRepository>();
        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object,
            mockPasswordHasher.Object,
            DefaultPasswordResetOptions);

        var resetRequest = new ResetPasswordRequest
        {
            Token = "kullanilmis-reset-token",
            NewPassword = "YeniSifre123!"
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ValidationException>(
            () => authService.ResetPasswordAsync(resetRequest));
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenValid_HashesNewPasswordAndRevokesAllRefreshTokens()
    {
        // ARRANGE
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "old-hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow,
            RefreshTokens = new List<RefreshToken>
            {
                new() { Id = Guid.NewGuid(), Token = "rt1", IsRevoked = false, ExpiresAt = DateTime.UtcNow.AddDays(1) },
                new() { Id = Guid.NewGuid(), Token = "rt2", IsRevoked = false, ExpiresAt = DateTime.UtcNow.AddDays(2) }
            }
        };

        var validResetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Token = "valid-reset-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        mockPasswordResetTokenRepository
            .Setup(repo => repo.GetByTokenAsync("valid-reset-token"))
            .ReturnsAsync(validResetToken);

        var mockUserRepository = new Mock<IUserRepository>();
        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();
        mockPasswordHasher.Setup(h => h.HashPassword("BrandNewPassword123!")).Returns("new-hashed-password");

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object,
            mockPasswordHasher.Object,
            DefaultPasswordResetOptions);

        var resetRequest = new ResetPasswordRequest
        {
            Token = "valid-reset-token",
            NewPassword = "BrandNewPassword123!"
        };

        // ACT
        await authService.ResetPasswordAsync(resetRequest);

        // ASSERT
        Assert.Equal("new-hashed-password", user.PasswordHash);
        Assert.True(validResetToken.IsUsed);
        Assert.All(user.RefreshTokens, rt => Assert.True(rt.IsRevoked));
        mockPasswordResetTokenRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenCurrentPasswordIsIncorrect_ThrowsValidationException()
    {
        // ARRANGE
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "old-hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(repo => repo.GetByIdAsync(userId)).ReturnsAsync(user);

        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();
        mockPasswordHasher.Setup(h => h.VerifyPassword("WrongPassword!", "old-hash")).Returns(false);

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object,
            mockPasswordHasher.Object,
            DefaultPasswordResetOptions);

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "WrongPassword!",
            NewPassword = "NewValidPassword123!"
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ValidationException>(() => authService.ChangePasswordAsync(userId, request));
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenValid_HashesNewPasswordAndRevokesRefreshTokens()
    {
        // ARRANGE
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "old-hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow,
            RefreshTokens = new List<RefreshToken>
            {
                new() { Id = Guid.NewGuid(), Token = "rt1", IsRevoked = false, ExpiresAt = DateTime.UtcNow.AddDays(1) },
                new() { Id = Guid.NewGuid(), Token = "rt2", IsRevoked = false, ExpiresAt = DateTime.UtcNow.AddDays(1) }
            }
        };

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(repo => repo.GetByIdAsync(userId)).ReturnsAsync(user);

        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();
        mockPasswordHasher.Setup(h => h.VerifyPassword("CorrectPassword!", "old-hash")).Returns(true);
        mockPasswordHasher.Setup(h => h.HashPassword("NewPassword123!")).Returns("new-hash");

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object,
            mockPasswordHasher.Object,
            DefaultPasswordResetOptions);

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "CorrectPassword!",
            NewPassword = "NewPassword123!"
        };

        // ACT
        await authService.ChangePasswordAsync(userId, request);

        // ASSERT
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.All(user.RefreshTokens, rt => Assert.True(rt.IsRevoked));
        mockUserRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }
}