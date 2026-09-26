using Microsoft.Extensions.DependencyInjection;
using TodoApp.Application.Interfaces;
using TodoApp.Application.Services;

namespace TodoApp.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Application katmanına ait iş mantığı servislerinin (Application Services) DI kaydını yapar.
    /// Clean Architecture prensiplerine göre her katman kendi servislerini paketlemelidir.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITodoItemService, TodoItemService>();
        services.AddScoped<ITodoListService, TodoListService>();
        services.AddScoped<ISubTaskService, SubTaskService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<ITaskShareService, TaskShareService>();
        services.AddScoped<ITaskAuthorizationService, TaskAuthorizationService>();
        services.AddScoped<ITaskTransferService, TaskTransferService>();
        services.AddScoped<ITodoItemActivityService, TodoItemActivityService>();

        return services;
    }
}
