using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OtpNet;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Services;
using TodoApp.Application.Settings;
using TodoApp.Domain.Entities;
using System.Security.Claims;
using TodoApp.Domain.Exceptions;
using Xunit;

namespace TodoApp.Application.Tests.Services;

public class AuthServiceTwoFactorTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly AuthService _authService;

    public AuthServiceTwoFactorTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _jwtTokenGeneratorMock = new Mock<IJwtTokenGenerator>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        var emailSenderMock = new Mock<IEmailSender>();
        var passwordResetRepoMock = new Mock<IPasswordResetTokenRepository>();
        var passwordSettingsMock = new Mock<IOptions<PasswordResetSettings>>();
        passwordSettingsMock.Setup(x => x.Value).Returns(new PasswordResetSettings());

        _authService = new AuthService(
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _jwtTokenGeneratorMock.Object,
            emailSenderMock.Object,
            passwordResetRepoMock.Object,
            _passwordHasherMock.Object,
            passwordSettingsMock.Object,
            Mock.Of<ILogger<AuthService>>()
        );
    }

    [Fact]
    public async Task EnableTwoFactorAsync_ShouldGenerateSecretAndQrUri()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@example.com" };
        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _authService.EnableTwoFactorAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.Secret));
        Assert.Contains("otpauth://totp/TodoApp:test%40example.com", result.QrCodeUri);
        Assert.Contains(result.Secret, result.QrCodeUri);

        Assert.False(user.TwoFactorEnabled); // Henüz verify edilmediği için false kalmalı
        Assert.Equal(result.Secret, user.TwoFactorSecret);
    }

    [Fact]
    public async Task VerifyTwoFactorSetupAsync_WithValidCode_ShouldEnableTwoFactor()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        var user = new User { Id = userId, TwoFactorSecret = secret, TwoFactorEnabled = false };

        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        var totp = new Totp(Base32Encoding.ToBytes(secret));
        var validCode = totp.ComputeTotp();

        var request = new TwoFactorVerifyRequest { Code = validCode };

        // Act
        await _authService.VerifyTwoFactorSetupAsync(userId, request);

        // Assert
        Assert.True(user.TwoFactorEnabled);
        _userRepositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task VerifyTwoFactorSetupAsync_WithInvalidCode_ShouldThrowValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        var user = new User { Id = userId, TwoFactorSecret = secret, TwoFactorEnabled = false };

        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        var request = new TwoFactorVerifyRequest { Code = "000000" }; // Hatalı kod

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _authService.VerifyTwoFactorSetupAsync(userId, request));
        Assert.Equal("Geçersiz veya süresi dolmuş kod.", exception.Message);
    }

    [Fact]
    public async Task LoginAsync_WhenTwoFactorEnabled_ReturnsRequiresTwoFactorAndTwoFactorToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "hashed-password",
            TwoFactorEnabled = true,
            TwoFactorSecret = "secret"
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync("test@example.com")).ReturnsAsync(user);
        _passwordHasherMock.Setup(x => x.VerifyPassword("Password123!", "hashed-password")).Returns(true);
        _jwtTokenGeneratorMock.Setup(x => x.GenerateTwoFactorTempToken(user)).Returns(("temp-2fa-token-xyz", DateTime.UtcNow.AddMinutes(5)));

        var request = new LoginRequest { Email = "test@example.com", Password = "Password123!" };

        // Act
        var response = await _authService.LoginAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.RequiresTwoFactor);
        Assert.Equal("temp-2fa-token-xyz", response.TwoFactorToken);
        Assert.Equal(userId, response.UserId);
        Assert.True(string.IsNullOrEmpty(response.Token));
    }

    [Fact]
    public async Task LoginWithTwoFactorAsync_WithValidTempTokenAndCode_ReturnsAuthResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var stamp = Guid.NewGuid();
        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            TwoFactorEnabled = true,
            TwoFactorSecret = secret,
            SecurityStamp = stamp
        };

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("security_stamp", stamp.ToString()),
            new Claim("purpose", "two_factor_pre_auth")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        _jwtTokenGeneratorMock.Setup(x => x.ValidateTwoFactorTempToken("valid-temp-token")).Returns(principal);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _jwtTokenGeneratorMock.Setup(x => x.GenerateToken(user)).Returns("access-jwt-token");
        _jwtTokenGeneratorMock.Setup(x => x.GenerateRefreshToken()).Returns(("refresh-token-xyz", DateTime.UtcNow.AddDays(7)));

        var totp = new Totp(Base32Encoding.ToBytes(secret));
        var validCode = totp.ComputeTotp();

        var request = new TwoFactorLoginRequest
        {
            TwoFactorToken = "valid-temp-token",
            Code = validCode
        };

        // Act
        var response = await _authService.LoginWithTwoFactorAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("access-jwt-token", response.Token);
        Assert.Equal("refresh-token-xyz", response.RefreshToken);
        Assert.False(response.RequiresTwoFactor);
    }

    [Fact]
    public async Task LoginWithTwoFactorAsync_WithInvalidTempToken_ThrowsValidationException()
    {
        // Arrange
        _jwtTokenGeneratorMock.Setup(x => x.ValidateTwoFactorTempToken("invalid-temp-token")).Returns((ClaimsPrincipal?)null);

        var request = new TwoFactorLoginRequest
        {
            TwoFactorToken = "invalid-temp-token",
            Code = "123456"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _authService.LoginWithTwoFactorAsync(request));
        Assert.Contains("Geçersiz veya süresi dolmuş 2FA oturumu", ex.Message);
    }

    [Fact]
    public async Task LoginWithTwoFactorAsync_WithSecurityStampMismatch_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldStamp = Guid.NewGuid();
        var currentStamp = Guid.NewGuid();
        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

        var user = new User
        {
            Id = userId,
            TwoFactorEnabled = true,
            TwoFactorSecret = secret,
            SecurityStamp = currentStamp // Yeni stamp
        };

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("security_stamp", oldStamp.ToString()) // Eski stamp token içinde kalmış
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        _jwtTokenGeneratorMock.Setup(x => x.ValidateTwoFactorTempToken("temp-token")).Returns(principal);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        var request = new TwoFactorLoginRequest
        {
            TwoFactorToken = "temp-token",
            Code = "123456"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _authService.LoginWithTwoFactorAsync(request));
        Assert.Contains("Oturum geçerliliğini yitirdi", ex.Message);
    }

    [Fact]
    public async Task LoginWithTwoFactorAsync_WithInvalidTotpCode_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var stamp = Guid.NewGuid();
        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

        var user = new User
        {
            Id = userId,
            TwoFactorEnabled = true,
            TwoFactorSecret = secret,
            SecurityStamp = stamp
        };

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("security_stamp", stamp.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        _jwtTokenGeneratorMock.Setup(x => x.ValidateTwoFactorTempToken("temp-token")).Returns(principal);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        var request = new TwoFactorLoginRequest
        {
            TwoFactorToken = "temp-token",
            Code = "000000" // Wrong code
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _authService.LoginWithTwoFactorAsync(request));
        Assert.Equal("Geçersiz veya süresi dolmuş kod.", ex.Message);
    }
}
