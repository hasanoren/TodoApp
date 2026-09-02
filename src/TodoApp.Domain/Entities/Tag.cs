namespace TodoApp.Domain.Entities;

public class Tag
{
    public Guid Id { get; set; }

    // BR-021 & BR-023: Global etiket adı (Unique, case-insensitive)
    public string Name { get; set; } = string.Empty;

    // BR-023: Admin silinse bile Tag kalır (CreatedByUserId ON DELETE SET NULL)
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TodoItemTag> TodoItemTags { get; set; } = new List<TodoItemTag>();
}

