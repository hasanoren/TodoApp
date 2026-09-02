using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;

namespace TodoApp.Api.Controllers;

[Authorize]
[ApiController]
public class TagsController : ControllerBase
{
    private readonly ITagService _tagService;

    public TagsController(ITagService tagService)
    {
        _tagService = tagService;
    }

    // T4.1.2: POST /api/tags — Sadece Admin rolü etiket oluşturabilir (BR-022)
    [Authorize(Roles = "Admin")]
    [HttpPost("api/tags")]
    public async Task<IActionResult> Create(CreateTagRequest request)
    {
        var adminUserId = GetCurrentUserId();
        var result = await _tagService.CreateAsync(adminUserId, request);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    // T4.1.3: GET /api/tags — Herkes global etiketleri listeleyebilir (BR-021)
    [HttpGet("api/tags")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _tagService.GetAllAsync();
        return Ok(result);
    }

    // GET /api/todoitems/{taskId}/tags — Bir göreve atanmış etiketleri listeleme
    [HttpGet("api/todoitems/{taskId:guid}/tags")]
    public async Task<IActionResult> GetTagsByTaskId(Guid taskId)
    {
        var userId = GetCurrentUserId();
        var result = await _tagService.GetTagsByTaskIdAsync(userId, taskId);
        return Ok(result);
    }

    // GET /api/tags/{tagId}/todoitems — Belirli bir etikete sahip aktif görevleri listeleme
    [HttpGet("api/tags/{tagId:guid}/todoitems")]
    public async Task<IActionResult> GetTodoItemsByTagId(Guid tagId)
    {
        var userId = GetCurrentUserId();
        var result = await _tagService.GetTasksByTagIdAsync(userId, tagId);
        return Ok(result);
    }

    // T4.2.2: POST /api/todoitems/{taskId}/tags/{tagId} — Göreve etiket bağlama (BR-024)
    [HttpPost("api/todoitems/{taskId:guid}/tags/{tagId:guid}")]
    public async Task<IActionResult> AssignTagToTask(Guid taskId, Guid tagId)
    {
        var userId = GetCurrentUserId();
        await _tagService.AssignTagToTaskAsync(userId, taskId, tagId);
        return NoContent();
    }

    // T4.2.3: DELETE /api/todoitems/{taskId}/tags/{tagId} — Görevden etiketi kaldırma
    [HttpDelete("api/todoitems/{taskId:guid}/tags/{tagId:guid}")]
    public async Task<IActionResult> RemoveTagFromTask(Guid taskId, Guid tagId)
    {
        var userId = GetCurrentUserId();
        await _tagService.RemoveTagFromTaskAsync(userId, taskId, tagId);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        return userId;
    }
}

