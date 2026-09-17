namespace TodoApp.Application.DTOs;

public class TodoListResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ColorCode { get; set; }
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
}

