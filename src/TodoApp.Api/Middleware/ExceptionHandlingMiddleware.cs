using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Api.Middleware;

public class ExceptionHandlingMiddleware
{
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
            await _next(context);   // sıradaki middleware'i / Controller'ı çalıştır
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "İstek işlenirken bir hata oluştu: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = exception switch
        {
            UnauthorizedAccessException => HttpStatusCode.Unauthorized, // 401
            ValidationException => HttpStatusCode.BadRequest,           // 400
            ForbiddenException => HttpStatusCode.Forbidden,             // 403
            NotFoundException => HttpStatusCode.NotFound,               // 404
            ConflictException => HttpStatusCode.Conflict,               // 409
            _ => HttpStatusCode.InternalServerError                      // 500 — beklenmeyen her şey
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        object response;
        if (statusCode == HttpStatusCode.InternalServerError)
        {
            response = _environment.IsDevelopment()
                ? new { message = exception.Message, detail = exception.StackTrace }
                : new { message = "Beklenmeyen bir hata oluştu." };
        }
        else
        {
            response = new { message = exception.Message };
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}