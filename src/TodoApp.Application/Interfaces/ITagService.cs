using TodoApp.Application.DTOs;

namespace TodoApp.Application.Interfaces;

public interface ITagService
{
    Task<TagResponse> CreateAsync(Guid createdByUserId, CreateTagRequest request);
    Task<List<TagResponse>> GetAllAsync();
    Task<List<TagResponse>> GetTagsByTaskIdAsync(Guid userId, Guid taskId);
    Task<List<TodoItemResponse>> GetTasksByTagIdAsync(Guid userId, Guid tagId);
    Task AssignTagToTaskAsync(Guid userId, Guid taskId, Guid tagId);
    Task RemoveTagFromTaskAsync(Guid userId, Guid taskId, Guid tagId);
}

