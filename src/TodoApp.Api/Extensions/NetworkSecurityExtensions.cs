using Microsoft.AspNetCore.HttpOverrides;
using TodoApp.Application.Settings;

namespace TodoApp.Api.Extensions;

public static class NetworkSecurityExtensions
{
    public const string CorsPolicyName = "AllowFrontend";

    /// <summary>
    /// T12.2.2: CORS (Cross-Origin Resource Sharing) politikasını güvenli şekilde yapılandırır.
    /// Web frontend (React/Vite) ve Flutter Web istemcilerinin API'ye kontrollü erişimini sağlar.
    /// Güvenlik Sertleştirmesi: AllowCredentials kullanılırken wildcard (*) kesinlikle engellenir.
    /// </summary>
    public static IServiceCollection AddAppCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CorsSettings>(configuration.GetSection(CorsSettings.SectionName));

        var corsSettings = configuration
            .GetSection(CorsSettings.SectionName)
            .Get<CorsSettings>() ?? new CorsSettings();

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                var rawOrigins = corsSettings.AllowedOrigins.Length > 0
                    ? corsSettings.AllowedOrigins
                    : ["http://localhost:3000", "http://localhost:5173"];

                // Güvenlik Koruması: AllowCredentials ile '*' (wildcard) kullanımı CSRF riski taşır ve tarayıcılarca engellenir.
                var safeOrigins = rawOrigins.Where(o => o != "*").ToArray();

                policy.WithOrigins(safeOrigins.Length > 0 ? safeOrigins : ["http://localhost:3000", "http://localhost:5173"])
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return services;
    }

    /// <summary>
    /// T12.2.2 & T11.1.2: Ters Proxy (Reverse Proxy - Nginx, Cloudflare, Traefik, Azure ARR) arkasında
    /// istemcinin gerçek IP ve HTTPS protokolünü güvenle almak için ForwardedHeaders ayarlarını yapar.
    /// 
    /// Güvenlik Sertleştirmesi (IP Spoofing & Rate Limit Bypass Koruması):
    /// 'ForwardLimit = 1' belirlenerek yalnızca en dıştaki ters proxy'nin (Azure ARR) eklediği gerçek IP'ye güvenilir.
    /// Araya enjekte edilebilecek sahte X-Forwarded-For başlıkları yoksayılır.
    /// </summary>
    public static IServiceCollection AddAppForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            // Güvenlik: Yalnızca en dıştaki güvenilir ters proxy'nin eklediği son IP adresini dikkate al.
            // Bu sayede saldırganların sahte IP başlıklarıyla Rate Limiting'i (örn. 5 istek/dk) atlatması engellenir.
            options.ForwardLimit = 1;
        });

        return services;
    }
}
