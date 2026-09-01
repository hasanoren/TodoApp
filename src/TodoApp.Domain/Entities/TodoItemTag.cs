namespace TodoApp.Domain.Entities;

public class TodoItemTag
{
    // BR-024: Composite PK (TodoItemId + TagId) — Aynı Tag aynı Task'a iki kez eklenemez
    public Guid TodoItemId { get; set; }
    public TodoItem TodoItem { get; set; } = null!;

    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

