using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Settings;

namespace TodoApp.Api.Extensions;

public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// T12.2.1: JWT Kimlik Doğrulama, SecurityStamp Oturum Doğrulaması ve SignalR WebSocket Token Desteğini yapılandırır.
    /// Program.cs içerisindeki 70+ satırlık güvenlik karmaşasını izole eder.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // 1. Strongly-Typed Options Pattern & Fail-Fast Kontrolü
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        var jwtSettings = configuration
            .GetSection(JwtSettings.SectionName)
            .Get<JwtSettings>() ?? new JwtSettings();

        // Azure ortam değişkeni veya appsettings yedek kontrolü
        if (string.IsNullOrWhiteSpace(jwtSettings.Key) || jwtSettings.Key.Length < 32)
        {
            jwtSettings.Key = configuration["Jwt:Key"]
                ?? configuration["Jwt__Key"]
                ?? string.Empty;
        }

        // Fail-Fast: Üretim ortamında zayıf anahtarla açılmayı engelle
        if (string.IsNullOrWhiteSpace(jwtSettings.Key) || jwtSettings.Key.Length < 32)
        {
            if (environment.IsDevelopment())
            {
                jwtSettings.Key = "super_secret_jwt_key_that_is_at_least_32_characters_long_12345!";
            }
            else
            {
                throw new InvalidOperationException("Üretim (Production) ortamında geçerli bir JWT Secret Key (en az 32 karakter) yapılandırılmalıdır.");
            }
        }

        // 2. Authentication ve JwtBearer Pipeline Kaydı
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
                };

                options.Events = new JwtBearerEvents
                {
                    // WebSocket (SignalR) bağlantılarında token query string'den okunur
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },

                    // T10.2.2: JWT Token İptali / Güvenlik Damgası (SecurityStamp) Doğrulaması
                    OnTokenValidated = async context =>
                    {
                        var userRepo = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                        var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

                        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                        {
                            context.Fail("Geçersiz token bilgisi.");
                            return;
                        }

                        var user = await userRepo.GetByIdAsync(userId);
                        if (user == null)
                        {
                            context.Fail("Kullanıcı bulunamadı.");
                            return;
                        }

                        var tokenStamp = context.Principal?.FindFirst("security_stamp")?.Value;
                        if (string.IsNullOrEmpty(tokenStamp) || !Guid.TryParse(tokenStamp, out var stampGuid) || user.SecurityStamp != stampGuid)
                        {
                            context.Fail("Oturum süresi doldu veya güvenlik bilgileri değişti.");
                        }
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
