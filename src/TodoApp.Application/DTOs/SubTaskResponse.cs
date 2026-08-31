namespace TodoApp.Application.DTOs;

public class SubTaskResponse
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

