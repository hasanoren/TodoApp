namespace TodoApp.Domain.Entities;

public class TodoList
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? ColorCode { get; set; }

    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public bool IsDeleted { get; set; } = false;
    public Guid? DeletedByUserId { get; set; }
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TodoItem> TodoItems { get; set; } = new List<TodoItem>();
}

