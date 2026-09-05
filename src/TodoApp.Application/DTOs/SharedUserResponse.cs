namespace TodoApp.Application.DTOs;

public class SharedUserResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime SharedAt { get; set; }
}

