namespace TodoApp.Domain.Entities;

public class TaskShare
{
    public Guid TaskId { get; set; }
    public TodoItem Task { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime SharedAt { get; set; } = DateTime.UtcNow;
}

