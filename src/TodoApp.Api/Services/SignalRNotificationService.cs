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

    public async Task SendTaskSharedAsync(Guid userId, Guid taskId, string taskTitle)
    {
        var client = _hubContext.Clients.User(userId.ToString());
        await client.ReceiveNotification("Yeni Görev Paylaşımı", $"'{taskTitle}' adlı görev sizinle paylaşıldı.");
        await client.TaskShared(taskId, taskTitle);

        _logger.LogInformation("SignalR görev paylaşım bildirimi gönderildi. UserId: {UserId}, TaskId: {TaskId}", userId, taskId);
    }

    public async Task SendTaskUpdatedAsync(Guid userId, Guid taskId, string title, string message)
    {
        var client = _hubContext.Clients.User(userId.ToString());
        await client.ReceiveNotification(title, message);
        await client.TaskUpdated(taskId);

        _logger.LogInformation("SignalR görev güncelleme bildirimi gönderildi. UserId: {UserId}, TaskId: {TaskId}, Title: {Title}", userId, taskId, title);
    }

    public async Task SendTransferRequestedAsync(Guid userId, Guid requestId, string taskTitle)
    {
        var client = _hubContext.Clients.User(userId.ToString());
        await client.ReceiveNotification("Görev Devir Talebi", $"'{taskTitle}' adlı görevin sahiplik devri teklifi size iletildi.");
        await client.TransferRequested(requestId, taskTitle);

        _logger.LogInformation("SignalR devir talebi bildirimi gönderildi. UserId: {UserId}, RequestId: {RequestId}", userId, requestId);
    }
}

