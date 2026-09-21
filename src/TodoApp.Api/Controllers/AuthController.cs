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
        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
            result.RefreshToken = string.Empty; // T10.2.4: XSS koruması için body'den temizle
        }
        return Ok(result);
    }

    [EnableRateLimiting("auth-login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
            result.RefreshToken = string.Empty; // T10.2.4: XSS koruması için body'den temizle
        }
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RefreshTokenRequest? request = null)
    {
        var refreshToken = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            Request.Cookies.TryGetValue("refreshToken", out refreshToken);
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Geçersiz İstek",
                Detail = "Refresh token cookie veya istek gövdesinde bulunamadı.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _authService.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = refreshToken });
        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
            result.RefreshToken = string.Empty; // T10.2.4: XSS koruması için body'den temizle
        }
        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RefreshTokenRequest? request = null)
    {
        var refreshToken = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            Request.Cookies.TryGetValue("refreshToken", out refreshToken);
        }

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _authService.LogoutAsync(new RefreshTokenRequest { RefreshToken = refreshToken });
        }

        RemoveRefreshTokenCookie();
        return NoContent(); // 204 — başarılı ama dönecek içerik yok
    }

    [EnableRateLimiting("auth-forgot-password")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await _authService.ForgotPasswordAsync(request);
        return Ok(new { message = "Eğer bu e-posta adresi kayıtlıysa, şifre sıfırlama bağlantısı gönderildi." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request)
    {
        await _authService.ResetPasswordAsync(request);

        return Ok(new { message = "Şifreniz başarıyla değiştirildi." });
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
        if (!string.IsNullOrEmpty(response.RefreshToken))
        {
            SetRefreshTokenCookie(response.RefreshToken);
            response.RefreshToken = string.Empty; // T10.2.4: XSS koruması için body'den temizle
        }
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

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7),
            Path = "/"
        };
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

    private void RemoveRefreshTokenCookie()
    {
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            Path = "/",
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Strict
        });
    }

    private Guid GetUserId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (claim == null) throw new UnauthorizedAccessException();
        return Guid.Parse(claim.Value);
    }
}