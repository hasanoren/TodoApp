using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using TodoApp.Infrastructure.Data;

using Microsoft.IdentityModel.Tokens;
using System.Text;
using TodoApp.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using FluentValidation;
using FluentValidation.AspNetCore;
using TodoApp.Application.Validators;
using TodoApp.Api.Extensions;
using TodoApp.Application;
using TodoApp.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// T8.3.3: Serilog Entegrasyonu & Yapılandırılmış Loglama
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "Doğrulama Hatası",
            Status = StatusCodes.Status400BadRequest,
            Detail = "Bir veya daha fazla alanda doğrulama hatası oluştu.",
            Instance = context.HttpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problemDetails)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT token girin."
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});



// ---- T12.1.1 & T12.1.2: Katman Bazlı Servis ve Altyapı Kayıtları ----
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// API Katmanına Özgü Bağımlılıklar
builder.Services.AddScoped<TodoApp.Application.Interfaces.INotificationService, TodoApp.Api.Services.SignalRNotificationService>();
builder.Services.AddHostedService<TodoApp.Api.BackgroundServices.TodoReminderService>();
builder.Services.AddSignalR();
// Options Pattern (Harici Servis Ayarları)
builder.Services.Configure<TodoApp.Application.Settings.SmtpSettings>(
    builder.Configuration.GetSection(TodoApp.Application.Settings.SmtpSettings.SectionName));
builder.Services.Configure<TodoApp.Application.Settings.PasswordResetSettings>(
    builder.Configuration.GetSection(TodoApp.Application.Settings.PasswordResetSettings.SectionName));

// ---- T12.2.1: JWT Kimlik Doğrulama & Yetkilendirme ----
builder.Services.AddJwtAuthentication(builder.Configuration, builder.Environment);

// ---- T12.2.2: Ağ Güvenliği, CORS & Ters Proxy Yapılandırması ----
builder.Services.AddAppCors(builder.Configuration);
builder.Services.AddAppForwardedHeaders();

// Hız Sınırlama & Sağlık Kontrolleri
builder.Services.AddAppRateLimiting();
builder.Services.AddAppHealthChecks();

var app = builder.Build();

// Ters proxy başlıkları tüm middleware'lerden önce işletilmelidir
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // T11.1.1: Test arayüzü yalnızca yerel geliştirme (Development) ortamında açık olmalı
    app.MapGet("/test-signalr", () => Results.Content(TodoApp.Api.Extensions.SignalRTestPage.Html, "text/html; charset=utf-8"));
}
app.UseMiddleware<ExceptionHandlingMiddleware>();   // ---- YENİ: en başta olmalı ----
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();

app.UseHttpsRedirection();
app.UseCors(NetworkSecurityExtensions.CorsPolicyName);

// T10.2.6: Rate limiter kullanıcının kimliğine erişebilmesi için Authentication önce çalıştırılmalıdır
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapAppHealthChecks();
app.MapControllers();
app.MapHub<TodoApp.Api.Hubs.TodoHub>("/hubs/todo");

// T11.1.3: Otomatik Veritabanı Migration (Uygulama açılışında şema kontrolü ve güncellemesi)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    if (dbContext.Database.IsSqlServer())
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        try
        {
            logger.LogInformation("Veritabanı migration kontrolü başlatılıyor...");
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Veritabanı güncel ve migration işlemi başarıyla tamamlandı.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Veritabanı migration işlemi sırasında kritik hata oluştu: {Message}", ex.Message);
            throw;
        }
    }
}

app.Run();