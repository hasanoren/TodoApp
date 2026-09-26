using TodoApp.Domain.Common;

namespace TodoApp.Domain.Entities;

public enum SubTaskStatus
{
    Open = 0,
    Completed = 1
}

public class SubTask : BaseAuditableEntity
{
    // BR-016: Bir alt görev mutlaka bir üst göreve bağlıdır (NOT NULL FK)
    public Guid TaskId { get; set; }
    public TodoItem Task { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public SubTaskStatus Status { get; set; } = SubTaskStatus.Open;
}
