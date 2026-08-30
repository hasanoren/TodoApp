using Moq;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Services;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;
using Xunit;

namespace TodoApp.Application.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ThrowsConflictException()
    {
        // ARRANGE

        // 1. Sahte bir kullanıcı oluşturuyoruz - "sistemde zaten kayıtlı" olduğunu simüle edecek
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashli-sifre",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        // 2. Sahte IUserRepository oluşturuyoruz
        var mockUserRepository = new Mock<IUserRepository>();

        // 3. "GetByEmailAsync bu email ile çağrılırsa, existingUser'ı döndür" diyoruz
        //    (yani "bu email zaten kayıtlı" senaryosunu simüle ediyoruz)
        mockUserRepository
            .Setup(repo => repo.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(existingUser);

        // 4. Diğer bağımlılıklar için de sahte nesneler oluşturuyoruz
        //    (bu testte gerçekten kullanılmayacaklar ama constructor'ın çalışması için gerekliler)
        var mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var mockJwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        var mockEmailSender = new Mock<IEmailSender>();
        var mockPasswordResetTokenRepository = new Mock<IPasswordResetTokenRepository>();

        // 5. AuthService'i, sahte nesnelerle kuruyoruz
        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object);

        var registerRequest = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "YeniSifre123!"
        };

        // ACT & ASSERT
        // RegisterAsync çağrıldığında ConflictException fırlamasını bekliyoruz
        await Assert.ThrowsAsync<ConflictException>(
            () => authService.RegisterAsync(registerRequest));
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsIncorrect_ThrowsValidationException()
    {
        // ARRANGE
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("DogruSifre123!"),
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

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object);

        var loginRequest = new LoginRequest
        {
            Email = "test@example.com",
            Password = "YanlisSifre!"   // gerçek şifre "DogruSifre123!" idi
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ValidationException>(
            () => authService.LoginAsync(loginRequest));
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
            ExpiresAt = DateTime.UtcNow.AddDays(-1),   // dün süresi dolmuş - geçmiş bir tarih
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

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object);

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
            ExpiresAt = DateTime.UtcNow.AddDays(5),   // hâlâ geçerli
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

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object);

        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = "gecerli-token"
        };

        // ACT
        var result = await authService.RefreshTokenAsync(refreshRequest);

        // ASSERT
        Assert.Equal("yeni-access-token", result.Token);
        Assert.Equal("yeni-refresh-token", result.RefreshToken);
        Assert.True(validToken.IsRevoked);   // eski token gerçekten iptal edilmiş mi
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
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),   // 10 dakika önce süresi dolmuş
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

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object);

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
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),   // süresi dolmamış ama...
            IsUsed = true,                                  // ...zaten kullanılmış
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

        var authService = new AuthService(
            mockUserRepository.Object,
            mockRefreshTokenRepository.Object,
            mockJwtTokenGenerator.Object,
            mockEmailSender.Object,
            mockPasswordResetTokenRepository.Object);

        var resetRequest = new ResetPasswordRequest
        {
            Token = "kullanilmis-reset-token",
            NewPassword = "YeniSifre123!"
        };

        // ACT & ASSERT
        await Assert.ThrowsAsync<ValidationException>(
            () => authService.ResetPasswordAsync(resetRequest));
    }
}