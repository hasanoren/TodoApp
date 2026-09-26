using Microsoft.AspNetCore.HttpOverrides;
using TodoApp.Application.Settings;

namespace TodoApp.Api.Extensions;

public static class NetworkSecurityExtensions
{
    public const string CorsPolicyName = "AllowFrontend";

    /// <summary>
    /// T12.2.2: CORS (Cross-Origin Resource Sharing) politikasını yapılandırır.
    /// Flutter mobil ve frontend uygulamalarının API'ye güvenli erişimini sağlar.
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
                var origins = corsSettings.AllowedOrigins.Length > 0
                    ? corsSettings.AllowedOrigins
                    : ["http://localhost:3000", "http://localhost:5173"];

                policy.WithOrigins(origins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return services;
    }

    /// <summary>
    /// T12.2.2 & T11.1.2: Ters Proxy (Reverse Proxy - Nginx, Cloudflare, Traefik, Azure ARR) arkasında
    /// istemcinin gerçek IP ve HTTPS protokolünü almak için ForwardedHeaders ayarlarını yapar.
    /// </summary>
    public static IServiceCollection AddAppForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }
}
