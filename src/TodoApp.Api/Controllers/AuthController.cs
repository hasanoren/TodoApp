using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;

namespace TodoApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [EnableRateLimiting("auth-register")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {

        var result = await _authService.RegisterAsync(request);
        return Ok(result);

    }

    [EnableRateLimiting("auth-login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request);
        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshTokenRequest request)
    {
        await _authService.LogoutAsync(request);
        return NoContent(); // 204 — başarılı ama dönecek içerik yok
    }

    [EnableRateLimiting("auth-forgot-password")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await _authService.ForgotPasswordAsync(request);
        return Ok(new { message = "Eğer bu e-posta adresi kayıtlıysa, şifre sıfırlama bağlantısı gönderildi." });
    }

    [HttpGet("reset-password")]
    public IActionResult ResetPasswordPage([FromQuery] string token)
    {
        var encodedToken = System.Net.WebUtility.HtmlEncode(token ?? string.Empty);

        var html = $"""
        <!DOCTYPE html>
        <html lang="tr">
        <head>
            <meta charset="UTF-8">
            <title>Şifre Sıfırla</title>
        </head>
        <body>
            <h2>Şifre Sıfırla</h2>

            <form method="post"
                  action="/api/Auth/reset-password">

                <input type="hidden"
                       name="Token"
                       value="{encodedToken}" />

                <label>Yeni Şifre:</label>
                <br />

                <input type="password"
                       name="NewPassword"
                       required />

                <br /><br />

                <button type="submit">
                    Şifreyi Değiştir
                </button>
            </form>
        </body>
        </html>
        """;

        return Content(html, "text/html");
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromForm] ResetPasswordRequest request)
    {
        await _authService.ResetPasswordAsync(request);

        return Content(
            "<h2>Şifreniz başarıyla değiştirildi.</h2>",
            "text/html");
    }

    [Authorize]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        await _authService.ChangePasswordAsync(userId, request);

        return NoContent();
    }

    [HttpPost("login-2fa")]
    public async Task<IActionResult> LoginWithTwoFactor([FromBody] TwoFactorLoginRequest request)
    {
        var response = await _authService.LoginWithTwoFactorAsync(request);
        return Ok(response);
    }

    [HttpPost("2fa/enable")]
    [Authorize]
    public async Task<IActionResult> EnableTwoFactor()
    {
        var userId = GetUserId();
        var response = await _authService.EnableTwoFactorAsync(userId);
        return Ok(response);
    }

    [HttpPost("2fa/verify")]
    [Authorize]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] TwoFactorVerifyRequest request)
    {
        var userId = GetUserId();
        await _authService.VerifyTwoFactorSetupAsync(userId, request);
        return Ok(new { message = "İki adımlı doğrulama başarıyla aktifleştirildi." });
    }

    [HttpPost("2fa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableTwoFactor([FromBody] TwoFactorVerifyRequest request)
    {
        var userId = GetUserId();
        await _authService.DisableTwoFactorAsync(userId, request);
        return Ok(new { message = "İki adımlı doğrulama devre dışı bırakıldı." });
    }

    private Guid GetUserId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (claim == null) throw new UnauthorizedAccessException();
        return Guid.Parse(claim.Value);
    }
}