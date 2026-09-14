using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "İstek işlenirken bir hata oluştu: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, type) = exception switch
        {
            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Yetkilendirme Hatası",
                "https://tools.ietf.org/html/rfc9110#section-15.5.2"),

            ValidationException => (
                HttpStatusCode.BadRequest,
                "Geçersiz İstek",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1"),

            ForbiddenException => (
                HttpStatusCode.Forbidden,
                "Erişim Reddedildi",
                "https://tools.ietf.org/html/rfc9110#section-15.5.4"),

            NotFoundException => (
                HttpStatusCode.NotFound,
                "Kayıt Bulunamadı",
                "https://tools.ietf.org/html/rfc9110#section-15.5.5"),

            ConflictException => (
                HttpStatusCode.Conflict,
                "Çakışma Hatası",
                "https://tools.ietf.org/html/rfc9110#section-15.5.10"),

            _ => (
                HttpStatusCode.InternalServerError,
                "Sunucu Hatası",
                "https://tools.ietf.org/html/rfc9110#section-15.6.1")
        };

        var isServerError = statusCode == HttpStatusCode.InternalServerError;
        var detail = isServerError && !_environment.IsDevelopment()
            ? "Beklenmeyen bir sunucu hatası oluştu."
            : exception.Message;

        var problemDetails = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = (int)statusCode,
            Detail = detail,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        if (isServerError && _environment.IsDevelopment())
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        if (exception is ValidationException valEx && valEx.Errors?.Count > 0)
        {
            problemDetails.Extensions["errors"] = valEx.Errors;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, JsonOptions));
    }
}