namespace TodoApp.Domain.Entities;

public class TodoItemActivity
{
    public Guid Id { get; set; }

    public Guid TodoItemId { get; set; }
    public TodoItem TodoItem { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

