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



// ---- YENİ: DbContext'i DI container'a kaydet ----
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? builder.Configuration["ConnectionStrings:DefaultConnection"]
        ?? builder.Configuration["ConnectionStrings__DefaultConnection"]
        ?? builder.Configuration["DefaultConnection"];

    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });
});

builder.Services.AddScoped<TodoApp.Application.Interfaces.IUserRepository, TodoApp.Infrastructure.Repositories.UserRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.IRefreshTokenRepository, TodoApp.Infrastructure.Repositories.RefreshTokenRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.IJwtTokenGenerator, TodoApp.Infrastructure.Services.JwtTokenGenerator>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.IPasswordHasher, TodoApp.Infrastructure.Services.BCryptPasswordHasher>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.IAuthService, TodoApp.Application.Services.AuthService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.IEmailSender, TodoApp.Infrastructure.Services.EmailSender>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.IPasswordResetTokenRepository, TodoApp.Infrastructure.Repositories.PasswordResetTokenRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITodoItemRepository, TodoApp.Infrastructure.Repositories.TodoItemRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITodoListRepository, TodoApp.Infrastructure.Repositories.TodoListRepository>();

builder.Services.AddScoped<TodoApp.Application.Interfaces.ITodoItemService, TodoApp.Application.Services.TodoItemService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITodoListService, TodoApp.Application.Services.TodoListService>();
builder.Services.AddHostedService<TodoApp.Api.BackgroundServices.TodoReminderService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ISubTaskRepository, TodoApp.Infrastructure.Repositories.SubTaskRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ISubTaskService, TodoApp.Application.Services.SubTaskService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITagRepository, TodoApp.Infrastructure.Repositories.TagRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITagService, TodoApp.Application.Services.TagService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITaskShareRepository, TodoApp.Infrastructure.Repositories.TaskShareRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITaskShareService, TodoApp.Application.Services.TaskShareService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITaskAuthorizationService, TodoApp.Application.Services.TaskAuthorizationService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.IOwnershipTransferRequestRepository, TodoApp.Infrastructure.Repositories.OwnershipTransferRequestRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITaskTransferService, TodoApp.Application.Services.TaskTransferService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITodoItemActivityRepository, TodoApp.Infrastructure.Repositories.TodoItemActivityRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITodoItemActivityService, TodoApp.Application.Services.TodoItemActivityService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.INotificationService, TodoApp.Api.Services.SignalRNotificationService>();
builder.Services.AddSignalR();
// ---- T8.2.2: Strongly-Typed Options Pattern ----
builder.Services.Configure<TodoApp.Application.Settings.JwtSettings>(
    builder.Configuration.GetSection(TodoApp.Application.Settings.JwtSettings.SectionName));
builder.Services.Configure<TodoApp.Application.Settings.SmtpSettings>(
    builder.Configuration.GetSection(TodoApp.Application.Settings.SmtpSettings.SectionName));
builder.Services.Configure<TodoApp.Application.Settings.PasswordResetSettings>(
    builder.Configuration.GetSection(TodoApp.Application.Settings.PasswordResetSettings.SectionName));
builder.Services.Configure<TodoApp.Application.Settings.CorsSettings>(
    builder.Configuration.GetSection(TodoApp.Application.Settings.CorsSettings.SectionName));

var corsSettings = builder.Configuration
    .GetSection(TodoApp.Application.Settings.CorsSettings.SectionName)
    .Get<TodoApp.Application.Settings.CorsSettings>() ?? new TodoApp.Application.Settings.CorsSettings();

const string corsPolicyName = "AllowFrontend";

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
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

var jwtSettings = builder.Configuration
    .GetSection(TodoApp.Application.Settings.JwtSettings.SectionName)
    .Get<TodoApp.Application.Settings.JwtSettings>()
    ?? new TodoApp.Application.Settings.JwtSettings();

if (string.IsNullOrWhiteSpace(jwtSettings.Key) || jwtSettings.Key.Length < 32)
{
    jwtSettings.Key = builder.Configuration["Jwt:Key"]
        ?? builder.Configuration["Jwt__Key"]
        ?? string.Empty;
}

if (string.IsNullOrWhiteSpace(jwtSettings.Key) || jwtSettings.Key.Length < 32)
{
    if (builder.Environment.IsDevelopment())
    {
        jwtSettings.Key = "super_secret_jwt_key_that_is_at_least_32_characters_long_12345!";
    }
    else
    {
        throw new InvalidOperationException("Üretim (Production) ortamında geçerli bir JWT Secret Key (en az 32 karakter) yapılandırılmalıdır.");
    }
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            OnTokenValidated = async context =>
            {
                // T10.2.2: JWT Token İptali / Güvenlik Damgası (SecurityStamp) Doğrulaması
                var userRepo = context.HttpContext.RequestServices.GetRequiredService<TodoApp.Application.Interfaces.IUserRepository>();
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

builder.Services.AddAuthorization();
builder.Services.AddAppRateLimiting();
builder.Services.AddAppHealthChecks();

// T11.1.2: Ters Proxy (Nginx, Cloudflare, Traefik, AWS ALB) arkasında gerçek IP ve HTTPS protokolünü almak için
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

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
app.UseCors(corsPolicyName);

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