using System.Net;
using System.Text.Json;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);   // sıradaki middleware'i / Controller'ı çalıştır
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Beklenmeyen bir hata oluştu.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async System.Threading.Tasks.Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = exception switch
        {
            ConflictException => HttpStatusCode.Conflict,       // 409
            NotFoundException => HttpStatusCode.NotFound,       // 404
            ForbiddenException => HttpStatusCode.Forbidden,     // 403
            ValidationException => HttpStatusCode.BadRequest,   // 400
            _ => HttpStatusCode.InternalServerError              // 500 — beklenmeyen her şey
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new { message = exception.Message };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}