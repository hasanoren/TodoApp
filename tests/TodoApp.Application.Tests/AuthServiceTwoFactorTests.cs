using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OtpNet;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Services;
using TodoApp.Application.Settings;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;
using Xunit;

namespace TodoApp.Application.Tests.Services;

public class AuthServiceTwoFactorTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly AuthService _authService;

    public AuthServiceTwoFactorTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        var refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        var jwtTokenGeneratorMock = new Mock<IJwtTokenGenerator>();
        var emailSenderMock = new Mock<IEmailSender>();
        var passwordResetRepoMock = new Mock<IPasswordResetTokenRepository>();
        var passwordHasherMock = new Mock<IPasswordHasher>();
        var passwordSettingsMock = new Mock<IOptions<PasswordResetSettings>>();
        passwordSettingsMock.Setup(x => x.Value).Returns(new PasswordResetSettings());

        _authService = new AuthService(
            _userRepositoryMock.Object,
            refreshTokenRepositoryMock.Object,
            jwtTokenGeneratorMock.Object,
            emailSenderMock.Object,
            passwordResetRepoMock.Object,
            passwordHasherMock.Object,
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
}
