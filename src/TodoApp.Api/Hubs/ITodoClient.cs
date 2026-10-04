namespace TodoApp.Api.Hubs;

public interface ITodoClient
{
    Task ReceiveNotification(string title, string message);
    Task TaskShared(Guid taskId, string taskTitle);
    Task TaskUpdated(Guid taskId);
    Task TransferRequested(Guid requestId, string taskTitle);
}

