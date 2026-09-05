using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;

namespace TodoApp.Api.Controllers;

[Authorize]
[ApiController]
public class TaskSharesController : ControllerBase
{
    private readonly ITaskShareService _taskShareService;

    public TaskSharesController(ITaskShareService taskShareService)
    {
        _taskShareService = taskShareService;
    }

    // T5.1.2: POST /api/todoitems/{taskId}/shares — Görevi bir kullanıcıyla paylaşma (BR-013)
    [HttpPost("api/todoitems/{taskId:guid}/shares")]
    public async Task<IActionResult> ShareTask(Guid taskId, ShareTaskRequest request)
    {
        var ownerUserId = GetCurrentUserId();
        await _taskShareService.ShareAsync(ownerUserId, taskId, request);
        return Ok(new { message = "Görev başarıyla paylaşıldı." });
    }

    // GET /api/todoitems/{taskId}/shares — Görevin paylaşıldığı kullanıcıları listeleme
    [HttpGet("api/todoitems/{taskId:guid}/shares")]
    public async Task<IActionResult> GetSharedUsers(Guid taskId)
    {
        var userId = GetCurrentUserId();
        var result = await _taskShareService.GetSharedUsersAsync(userId, taskId);
        return Ok(result);
    }

    // T5.1.6: DELETE /api/todoitems/{taskId}/shares/{userId} — Görev sahibinin paylaşımı kaldırması
    [HttpDelete("api/todoitems/{taskId:guid}/shares/{userId:guid}")]
    public async Task<IActionResult> RemoveShare(Guid taskId, Guid userId)
    {
        var ownerUserId = GetCurrentUserId();
        await _taskShareService.RemoveShareAsync(ownerUserId, taskId, userId);
        return NoContent();
    }

    // T5.1.7: DELETE /api/todoitems/{taskId}/shares/me — Paylaşılan kullanıcının kendi isteğiyle ayrılması (BR-028)
    [HttpDelete("api/todoitems/{taskId:guid}/shares/me")]
    public async Task<IActionResult> LeaveShare(Guid taskId)
    {
        var currentUserId = GetCurrentUserId();
        await _taskShareService.LeaveShareAsync(currentUserId, taskId);
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
