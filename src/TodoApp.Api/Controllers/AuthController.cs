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
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request);
        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
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

    [EnableRateLimiting("auth-reset-password")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request)
    {
        await _authService.ResetPasswordAsync(request);

        return Ok(new { message = "Şifreniz başarıyla değiştirildi." });
    }

    [HttpGet("/reset-password")]
    [AllowAnonymous]
    public IActionResult ResetPasswordLanding([FromQuery] string? token, [FromQuery] string? email)
    {
        var deepLink = $"todoapp://reset-password?token={Uri.EscapeDataString(token ?? "")}&email={Uri.EscapeDataString(email ?? "")}";
        var html = $$"""
        <!DOCTYPE html>
        <html lang="tr">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>TodoApp - Şifre Sıfırlama</title>
            <style>
                body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; display: flex; justify-content: center; align-items: center; min-height: 100vh; margin: 0; background: #f8fafc; color: #1e293b; }
                .card { background: white; padding: 2.5rem; border-radius: 16px; box-shadow: 0 10px 25px -5px rgba(0,0,0,0.1); text-align: center; max-width: 420px; width: 90%; }
                .btn { display: inline-block; padding: 14px 28px; background: #2563eb; color: white; text-decoration: none; border-radius: 8px; font-weight: 600; margin-top: 1.5rem; }
            </style>
            <script>
                window.location.href = "{{deepLink}}";
            </script>
        </head>
        <body>
            <div class="card">
                <h2>TodoApp</h2>
                <p>Şifrenizi sıfırlamak için mobil uygulamanız açılıyor...</p>
                <p style="font-size: 0.9rem; color: #64748b;">Uygulama otomatik olarak açılmadıysa aşağıdaki butona dokunun:</p>
                <a class="btn" href="{{deepLink}}">Uygulamayı Aç</a>
            </div>
        </body>
        </html>
        """;
        return Content(html, "text/html; charset=utf-8");
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

    [EnableRateLimiting("auth-2fa-verify")]
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