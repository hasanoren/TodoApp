using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace TodoApp.Api.Extensions;

public static class RateLimiterExtensions
{
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/problem+json";

                var problemDetails = new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc6585#section-4",
                    Title = "Çok Fazla İstek Yapıldı",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "İstek limiti aşıldı. Lütfen daha sonra tekrar deneyin.",
                    Instance = context.HttpContext.Request.Path
                };
                problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    problemDetails,
                    (JsonSerializerOptions?)null,
                    "application/problem+json",
                    cancellationToken: token);
            };

            // Politika Tanımları: Tekrarlayan partition kodları AddIpPolicy ile tek satıra indirildi
            options.AddIpPolicy("auth-login", permitLimit: 5);
            options.AddIpPolicy("auth-register", permitLimit: 3);
            options.AddIpPolicy("auth-forgot-password", permitLimit: 2);
        });

        return services;
    }

    private static void AddIpPolicy(this RateLimiterOptions options, string policyName, int permitLimit, int windowMinutes = 1)
    {
        options.AddPolicy(policyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                GetClientIp(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(windowMinutes),
                    QueueLimit = 0
                }));
    }

    private static string GetClientIp(HttpContext httpContext)
    {
        var forwardedHeader = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedHeader))
        {
            return forwardedHeader.Split(',')[0].Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

