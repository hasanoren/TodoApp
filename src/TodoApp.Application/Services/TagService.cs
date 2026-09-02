using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class TagService : ITagService
{
    private readonly ITagRepository _tagRepository;
    private readonly ITodoItemRepository _todoItemRepository;

    public TagService(
        ITagRepository tagRepository,
        ITodoItemRepository todoItemRepository)
    {
        _tagRepository = tagRepository;
        _todoItemRepository = todoItemRepository;
    }

    public async Task<TagResponse> CreateAsync(Guid createdByUserId, CreateTagRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Etiket adı boş olamaz.");
        }

        var trimmedName = request.Name.Trim();

        // BR-021: Case-insensitive unique kontrolü
        var existingTag = await _tagRepository.GetByNameAsync(trimmedName);
        if (existingTag is not null)
        {
            throw new ConflictException("Bu isimde bir etiket zaten mevcut.");
        }

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        await _tagRepository.AddAsync(tag);
        await _tagRepository.SaveChangesAsync();

        return MapToResponse(tag);
    }

    public async Task<List<TagResponse>> GetAllAsync()
    {
        // BR-021: Global etiket listesi
        var tags = await _tagRepository.GetAllAsync();
        return tags.Select(MapToResponse).ToList();
    }

    public async Task<List<TagResponse>> GetTagsByTaskIdAsync(Guid userId, Guid taskId)
    {
        var task = await _todoItemRepository.GetByIdAsync(taskId);

        // BR-029: Görev bulunamazsa veya kullanıcı yetkisizse 404
        if (task is null || task.OwnerId != userId || task.IsDeleted)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        var tags = await _tagRepository.GetTagsByTodoItemIdAsync(taskId);
        return tags.Select(MapToResponse).ToList();
    }

    public async Task AssignTagToTaskAsync(Guid userId, Guid taskId, Guid tagId)
    {
        var task = await _todoItemRepository.GetByIdAsync(taskId);

        // BR-029: Yetkisiz erişimde 404
        if (task is null || task.OwnerId != userId)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        // BR-012: Silinmiş göreve etiket eklenemez
        if (task.IsDeleted)
        {
            throw new ValidationException("Silinmiş bir göreve etiket eklenemez.");
        }

        var tag = await _tagRepository.GetByIdAsync(tagId);
        if (tag is null)
        {
            throw new NotFoundException("Etiket bulunamadı.");
        }

        // BR-024: Aynı etiket aynı göreve iki kez eklenemez
        var existingRelation = await _tagRepository.GetTodoItemTagAsync(taskId, tagId);
        if (existingRelation is not null)
        {
            throw new ConflictException("Bu etiket zaten bu göreve atanmış.");
        }

        var todoItemTag = new TodoItemTag
        {
            TodoItemId = taskId,
            TagId = tagId,
            AssignedAt = DateTime.UtcNow
        };

        await _tagRepository.AddTodoItemTagAsync(todoItemTag);
        await _tagRepository.SaveChangesAsync();
    }

    public async Task RemoveTagFromTaskAsync(Guid userId, Guid taskId, Guid tagId)
    {
        var task = await _todoItemRepository.GetByIdAsync(taskId);

        // BR-029: Yetkisiz erişimde 404
        if (task is null || task.OwnerId != userId)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        var relation = await _tagRepository.GetTodoItemTagAsync(taskId, tagId);
        if (relation is null)
        {
            throw new NotFoundException("Bu etiket bu görevde bulunamadı.");
        }

        _tagRepository.RemoveTodoItemTag(relation);
        await _tagRepository.SaveChangesAsync();
    }

    public async Task<List<TodoItemResponse>> GetTasksByTagIdAsync(Guid userId, Guid tagId)
    {
        var tag = await _tagRepository.GetByIdAsync(tagId);
        if (tag is null)
        {
            throw new NotFoundException("Etiket bulunamadı.");
        }

        var tasks = await _tagRepository.GetTodoItemsByTagIdAsync(userId, tagId);
        return tasks.Select(MapToTodoItemResponse).ToList();
    }

    private static TagResponse MapToResponse(Tag tag)
    {
        return new TagResponse
        {
            Id = tag.Id,
            Name = tag.Name,
            CreatedAt = tag.CreatedAt
        };
    }

    private static TodoItemResponse MapToTodoItemResponse(TodoItem todoItem)
    {
        return new TodoItemResponse
        {
            Id = todoItem.Id,
            Title = todoItem.Title,
            Description = todoItem.Description,
            DueDate = todoItem.DueDate,
            Status = todoItem.Status.ToString(),
            OwnerId = todoItem.OwnerId,
            CompletedByUserId = todoItem.CompletedByUserId,
            CompletedAt = todoItem.CompletedAt,
            CreatedAt = todoItem.CreatedAt,
            SubTasks = new List<SubTaskResponse>(), // Liste görünümünde alt görevler çekilmez (hafif payload)
            Tags = todoItem.TodoItemTags?.Select(tit => new TagResponse
            {
                Id = tit.Tag?.Id ?? tit.TagId,
                Name = tit.Tag?.Name ?? string.Empty,
                CreatedAt = tit.Tag?.CreatedAt ?? tit.AssignedAt
            }).ToList() ?? new List<TagResponse>()
        };
    }
}

