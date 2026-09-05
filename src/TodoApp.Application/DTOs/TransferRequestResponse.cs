namespace TodoApp.Application.DTOs;

public class TransferRequestResponse
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public Guid FromUserId { get; set; }
    public string FromUserEmail { get; set; } = string.Empty;
    public Guid ToUserId { get; set; }
    public string ToUserEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

