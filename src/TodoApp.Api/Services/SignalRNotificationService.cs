using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TodoApp.Api.Hubs;
using TodoApp.Application.Interfaces;

namespace TodoApp.Api.Services;

public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<TodoHub, ITodoClient> _hubContext;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(IHubContext<TodoHub, ITodoClient> hubContext, ILogger<SignalRNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendNotificationAsync(Guid userId, string title, string message)
    {
        // Kime gönderileceği (SignalR NameIdentifier claim'i üzerinden otomatik eşler)
        await _hubContext.Clients.User(userId.ToString())
            .ReceiveNotification(title, message);

        _logger.LogInformation("SignalR bildirimi gönderildi. UserId: {UserId}, Title: {Title}", userId, title);
    }
}

