using FluentValidation.TestHelper;
using TodoApp.Application.DTOs;
using TodoApp.Application.Validators;
using Xunit;

namespace TodoApp.Application.Tests.ValidatorTests;

public class AuthValidatorTests
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly LoginRequestValidator _loginValidator = new();
    private readonly ForgotPasswordRequestValidator _forgotPasswordValidator = new();
    private readonly ResetPasswordRequestValidator _resetPasswordValidator = new();
    private readonly ChangePasswordRequestValidator _changePasswordValidator = new();
    private readonly RefreshTokenRequestValidator _refreshTokenValidator = new();

    // --- RegisterRequestValidator Tests ---

    [Fact]
    public void RegisterValidator_WhenValid_ShouldNotHaveErrors()
    {
        var model = new RegisterRequest
        {
            Email = "user@example.com",
            Password = "Password123!"
        };

        var result = _registerValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-email")]
    [InlineData("user@")]
    public void RegisterValidator_WhenEmailIsInvalid_ShouldHaveError(string email)
    {
        var model = new RegisterRequest
        {
            Email = email,
            Password = "Password123!"
        };

        var result = _registerValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]                   // Boş
    [InlineData("Short1!")]            // < 8 karakter
    [InlineData("alllowercase123!")]   // Büyük harf yok
    [InlineData("ALLUPPERCASE123!")]   // Küçük harf yok
    [InlineData("NoDigitsAtAll!")]      // Rakam yok
    [InlineData("NoSpecialChar123")]   // Özel karakter yok
    public void RegisterValidator_WhenPasswordIsWeak_ShouldHaveError(string password)
    {
        var model = new RegisterRequest
        {
            Email = "user@example.com",
            Password = password
        };

        var result = _registerValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void RegisterValidator_WhenPasswordExceeds128Chars_ShouldHaveError()
    {
        // BCrypt DoS koruması: 128 karakterden uzun şifreler engellenmeli
        var longPassword = new string('A', 130) + "a1!";
        var model = new RegisterRequest
        {
            Email = "user@example.com",
            Password = longPassword
        };

        var result = _registerValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    // --- LoginRequestValidator Tests ---

    [Fact]
    public void LoginValidator_WhenValid_ShouldNotHaveErrors()
    {
        var model = new LoginRequest
        {
            Email = "user@example.com",
            Password = "Password123!"
        };

        var result = _loginValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void LoginValidator_WhenEmpty_ShouldHaveErrors()
    {
        var model = new LoginRequest { Email = "", Password = "" };
        var result = _loginValidator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    // --- ForgotPasswordRequestValidator Tests ---

    [Fact]
    public void ForgotPasswordValidator_WhenValid_ShouldNotHaveErrors()
    {
        var model = new ForgotPasswordRequest { Email = "user@example.com" };
        var result = _forgotPasswordValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ForgotPasswordValidator_WhenInvalidEmail_ShouldHaveError()
    {
        var model = new ForgotPasswordRequest { Email = "invalid" };
        var result = _forgotPasswordValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // --- ResetPasswordRequestValidator Tests ---

    [Fact]
    public void ResetPasswordValidator_WhenValid_ShouldNotHaveErrors()
    {
        var model = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "NewPassword123!"
        };

        var result = _resetPasswordValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ResetPasswordValidator_WhenEmptyTokenOrWeakPassword_ShouldHaveErrors()
    {
        var model = new ResetPasswordRequest
        {
            Token = "",
            NewPassword = "weak"
        };

        var result = _resetPasswordValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Token);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    // --- ChangePasswordRequestValidator Tests ---

    [Fact]
    public void ChangePasswordValidator_WhenValid_ShouldNotHaveErrors()
    {
        var model = new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword123!"
        };

        var result = _changePasswordValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ChangePasswordValidator_WhenNewPasswordSameAsCurrent_ShouldHaveError()
    {
        var model = new ChangePasswordRequest
        {
            CurrentPassword = "SamePassword123!",
            NewPassword = "SamePassword123!"
        };

        var result = _changePasswordValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    // --- RefreshTokenRequestValidator Tests ---

    [Fact]
    public void RefreshTokenValidator_WhenValid_ShouldNotHaveErrors()
    {
        var model = new RefreshTokenRequest { RefreshToken = "valid-token-string" };
        var result = _refreshTokenValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RefreshTokenValidator_WhenEmpty_ShouldHaveError()
    {
        var model = new RefreshTokenRequest { RefreshToken = "" };
        var result = _refreshTokenValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }
}

