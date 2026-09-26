using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TodoApp.Application.Interfaces;
using TodoApp.Infrastructure.Data;
using TodoApp.Infrastructure.Repositories;
using TodoApp.Infrastructure.Services;

namespace TodoApp.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Infrastructure katmanına ait Veritabanı (EF Core), Repository'ler ve harici servislerin (Mail, Hash, Token) DI kaydını yapar.
    /// T12.1.1 & T12.1.2: Veritabanı ve altyapı bağımlılıkları Program.cs'ten izole edilir.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // T12.1.2: Veritabanı ve Connection Resiliency (Azure SQL Retry) Konfigürasyonu
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["ConnectionStrings:DefaultConnection"]
            ?? configuration["ConnectionStrings__DefaultConnection"]
            ?? configuration["DefaultConnection"];

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });
        });

        // Repository Kayıtları
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<ITodoItemRepository, TodoItemRepository>();
        services.AddScoped<ITodoListRepository, TodoListRepository>();
        services.AddScoped<ISubTaskRepository, SubTaskRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ITaskShareRepository, TaskShareRepository>();
        services.AddScoped<IOwnershipTransferRequestRepository, OwnershipTransferRequestRepository>();
        services.AddScoped<ITodoItemActivityRepository, TodoItemActivityRepository>();

        // Harici Servis Uygulamaları (Cross-cutting / Infrastructure Concerns)
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IEmailSender, EmailSender>();

        return services;
    }
}
