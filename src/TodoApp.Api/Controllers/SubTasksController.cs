using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;

namespace TodoApp.Api.Controllers;

[Authorize]
[ApiController]
public class SubTasksController : ControllerBase
{
    private readonly ISubTaskService _subTaskService;

    public SubTasksController(ISubTaskService subTaskService)
    {
        _subTaskService = subTaskService;
    }

    // T3.1.2: POST /api/todoitems/{taskId}/subtasks — Yeni alt görev ekleme
    [HttpPost("api/todoitems/{taskId:guid}/subtasks")]
    public async Task<IActionResult> Create(Guid taskId, CreateSubTaskRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _subTaskService.CreateAsync(userId, taskId, request);
        return CreatedAtAction(nameof(GetByTaskId), new { taskId }, result);
    }

    // T3.1.3: GET /api/todoitems/{taskId}/subtasks — Alt görevleri listeleme
    [HttpGet("api/todoitems/{taskId:guid}/subtasks")]
    public async Task<IActionResult> GetByTaskId(Guid taskId)
    {
        var userId = GetCurrentUserId();
        var result = await _subTaskService.GetByTaskIdAsync(userId, taskId);
        return Ok(result);
    }

    // T3.1.4: PATCH /api/subtasks/{id}/complete — Alt görevi tamamlama/açma
    [HttpPatch("api/subtasks/{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var userId = GetCurrentUserId();
        var result = await _subTaskService.CompleteAsync(userId, id);
        return Ok(result);
    }

    // T3.1.5: DELETE /api/subtasks/{id} — Alt görevi silme
    [HttpDelete("api/subtasks/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        await _subTaskService.DeleteAsync(userId, id);
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

