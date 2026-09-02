namespace TodoApp.Application.DTOs;

public class TodoItemResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // İlişkili alt görevler ve etiketler
    public List<SubTaskResponse> SubTasks { get; set; } = new();
    public List<TagResponse> Tags { get; set; } = new();
}

