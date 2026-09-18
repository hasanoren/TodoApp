using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace TodoApp.Api.Hubs;

[Authorize]
public class TodoHub : Hub<ITodoClient>
{
    private readonly ILogger<TodoHub> _logger;

    public TodoHub(ILogger<TodoHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("Yeni SignalR bağlantısı kuruldu. UserId: {UserId}, ConnectionId: {ConnectionId}", userId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (exception != null)
        {
            _logger.LogWarning(exception, "SignalR bağlantısı hata ile koptu. UserId: {UserId}", userId);
        }
        else
        {
            _logger.LogInformation("SignalR bağlantısı sonlandı. UserId: {UserId}", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

