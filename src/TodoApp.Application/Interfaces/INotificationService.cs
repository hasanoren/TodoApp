namespace TodoApp.Application.Interfaces;

public interface INotificationService
{
    Task SendNotificationAsync(Guid userId, string title, string message);
    Task SendTaskSharedAsync(Guid userId, Guid taskId, string taskTitle);
    Task SendTaskUpdatedAsync(Guid userId, Guid taskId, string title, string message);
    Task SendTransferRequestedAsync(Guid userId, Guid requestId, string taskTitle);
}

