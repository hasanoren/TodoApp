using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TodoApp.Api.HealthChecks;

namespace TodoApp.Api.Extensions;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        return services;
    }

    public static WebApplication MapAppHealthChecks(this WebApplication app)
    {
        // Liveness: /health (Uygulama sürecinin ayakta olduğunu doğrular)
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false, // Harici bağımlılıkları kontrol etmez
            ResponseWriter = WriteJsonResponseAsync
        });

        // Readiness: /health/ready (Veritabanı bağlantısı dahil sistemin istek almaya hazır olduğunu doğrular)
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteJsonResponseAsync
        });

        return app;
    }

    private static async Task WriteJsonResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = MediaTypeNames.Application.Json;

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString(),
            entries = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    duration = entry.Value.Duration.ToString(),
                    exception = entry.Value.Exception?.Message
                })
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
