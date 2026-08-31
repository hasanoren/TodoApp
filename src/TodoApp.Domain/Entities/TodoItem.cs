namespace TodoApp.Domain.Entities;

public enum TodoItemStatus
{
    Open = 0,
    Completed = 1
}

public class TodoItem
{
    public Guid Id { get; set; }

    // BR-006: Bir görev bir sahibe aittir, owner NOT NULL
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public TodoItemStatus Status { get; set; } = TodoItemStatus.Open;

    // BR-015: Paylaşım kalksa bile Completed bilgisi korunur
    public Guid? CompletedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }

    // BR-008b: Paylaşılan kullanıcı silerse soft delete
    public bool IsDeleted { get; set; } = false;
    public Guid? DeletedByUserId { get; set; }
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // BR-007: Bir görev sıfır veya daha fazla alt göreve (SubTask) sahip olabilir
    public ICollection<SubTask> SubTasks { get; set; } = new List<SubTask>();
}

