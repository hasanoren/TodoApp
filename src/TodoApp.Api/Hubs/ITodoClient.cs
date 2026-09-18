namespace TodoApp.Api.Hubs;

public interface ITodoClient
{
    Task ReceiveNotification(string title, string message);
}

