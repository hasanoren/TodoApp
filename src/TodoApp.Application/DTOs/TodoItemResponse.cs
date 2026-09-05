namespace TodoApp.Application.DTOs;

public class TodoItemResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public bool IsOwner { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // İlişkili alt görevler, etiketler ve paylaşılan kullanıcılar
    public List<SubTaskResponse> SubTasks { get; set; } = new();
    public List<TagResponse> Tags { get; set; } = new();
    public List<SharedUserResponse> SharedWith { get; set; } = new();
}

