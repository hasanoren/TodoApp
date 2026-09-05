namespace TodoApp.Domain.Entities;

public enum TransferRequestStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Cancelled = 3
}

public class OwnershipTransferRequest
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }
    public TodoItem Task { get; set; } = null!;

    public Guid FromUserId { get; set; }
    public User FromUser { get; set; } = null!;

    public Guid ToUserId { get; set; }
    public User ToUser { get; set; } = null!;

    public TransferRequestStatus Status { get; set; } = TransferRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}

