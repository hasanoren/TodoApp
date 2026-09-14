using Microsoft.AspNetCore.Authentication.JwtBearer;

using Microsoft.EntityFrameworkCore;
using TodoApp.Infrastructure.Data;

using Microsoft.IdentityModel.Tokens;
using System.Text;
using TodoApp.Api.Middleware;
using Microsoft.OpenApi;
using FluentValidation;
using FluentValidation.AspNetCore;
using TodoApp.Application.Validators;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<TodoApp.Application.Interfaces.IUserRepository, TodoApp.Infrastructure.Repositories.UserRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.IRefreshTokenRepository, TodoApp.Infrastructure.Repositories.RefreshTokenRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.IJwtTokenGenerator, TodoApp.Infrastructure.Services.JwtTokenGenerator>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.IAuthService, TodoApp.Application.Services.AuthService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.IEmailSender, TodoApp.Infrastructure.Services.EmailSender>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.IPasswordResetTokenRepository, TodoApp.Infrastructure.Repositories.PasswordResetTokenRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITodoItemRepository, TodoApp.Infrastructure.Repositories.TodoItemRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITodoItemService, TodoApp.Application.Services.TodoItemService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ISubTaskRepository, TodoApp.Infrastructure.Repositories.SubTaskRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ISubTaskService, TodoApp.Application.Services.SubTaskService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITagRepository, TodoApp.Infrastructure.Repositories.TagRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITagService, TodoApp.Application.Services.TagService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITaskShareRepository, TodoApp.Infrastructure.Repositories.TaskShareRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITaskShareService, TodoApp.Application.Services.TaskShareService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITaskAuthorizationService, TodoApp.Application.Services.TaskAuthorizationService>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.IOwnershipTransferRequestRepository, TodoApp.Infrastructure.Repositories.OwnershipTransferRequestRepository>();
builder.Services.AddScoped<TodoApp.Application.Interfaces.ITaskTransferService, TodoApp.Application.Services.TaskTransferService>();

// ---- T8.2.2: Strongly-Typed Options Pattern ----
builder.Services.Configure<TodoApp.Application.Settings.JwtSettings>(
    builder.Configuration.GetSection(TodoApp.Application.Settings.JwtSettings.SectionName));
builder.Services.Configure<TodoApp.Application.Settings.SmtpSettings>(
    builder.Configuration.GetSection(TodoApp.Application.Settings.SmtpSettings.SectionName));
builder.Services.Configure<TodoApp.Application.Settings.PasswordResetSettings>(
    builder.Configuration.GetSection(TodoApp.Application.Settings.PasswordResetSettings.SectionName));

var jwtSettings = builder.Configuration
    .GetSection(TodoApp.Application.Settings.JwtSettings.SectionName)
    .Get<TodoApp.Application.Settings.JwtSettings>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

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
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();      // ---- YENİ: /swagger/v1/swagger.json dokümanını sunar ----
    app.UseSwaggerUI();    // ---- YENİ: /swagger adresinde görsel arayüzü açar ----

}
app.UseMiddleware<ExceptionHandlingMiddleware>();   // ---- YENİ: en başta olmalı ----

app.UseHttpsRedirection();

// ---- YENİ: Authentication, Authorization'dan ÖNCE gelmeli ----
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();