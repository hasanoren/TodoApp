using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Application.DTOs;
using TodoApp.Application.Services;

namespace TodoApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {

        var result = await _authService.RegisterAsync(request);
        return Ok(result);

    }

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

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await _authService.ForgotPasswordAsync(request);
        return Ok(new { message = "Eğer bu e-posta adresi kayıtlıysa, şifre sıfırlama bağlantısı gönderildi." });
    }

    [HttpGet("reset-password")]
    public IActionResult ResetPasswordPage([FromQuery] string token)
    {
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
                       value="{token}" />

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
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request)
    {
        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        await _authService.ChangePasswordAsync(
            userId,
            request);

        return NoContent();
    }
}