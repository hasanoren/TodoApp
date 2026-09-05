using TodoApp.Application.DTOs;

namespace TodoApp.Application.Interfaces;

public interface ITaskShareService
{
    Task ShareAsync(Guid ownerUserId, Guid taskId, ShareTaskRequest request);
    Task<List<SharedUserResponse>> GetSharedUsersAsync(Guid userId, Guid taskId);
    Task RemoveShareAsync(Guid ownerUserId, Guid taskId, Guid targetUserId);
    Task LeaveShareAsync(Guid sharedUserId, Guid taskId);
}
