namespace TodoApp.Domain.Entities;

public enum SubTaskStatus
{
    Open = 0,
    Completed = 1
}

public class SubTask
{
    public Guid Id { get; set; }

    // BR-016: Bir alt görev mutlaka bir üst göreve bağlıdır (NOT NULL FK)
    public Guid TaskId { get; set; }
    public TodoItem Task { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public SubTaskStatus Status { get; set; } = SubTaskStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

