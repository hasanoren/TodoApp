using TodoApp.Application.DTOs;

namespace TodoApp.Application.Interfaces;

public interface ITaskTransferService
{
    Task<TransferRequestResponse> CreateTransferRequestAsync(Guid currentOwnerId, Guid taskId, CreateTransferRequestDto dto);
    Task<List<TransferRequestResponse>> GetPendingRequestsAsync(Guid userId);
    Task AcceptTransferRequestAsync(Guid targetUserId, Guid requestId);
    Task RejectTransferRequestAsync(Guid targetUserId, Guid requestId);
    Task CancelTransferRequestAsync(Guid ownerUserId, Guid requestId);
}

