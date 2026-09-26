using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;

namespace TodoApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TodoItemsController : ControllerBase
{
    private readonly ITodoItemService _todoItemService;

    public TodoItemsController(ITodoItemService todoItemService)
    {
        _todoItemService = todoItemService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTodoItemRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _todoItemService.CreateAsync(userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] TodoItemFilterDto filter, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _todoItemService.GetAllAsync(userId, filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _todoItemService.GetByIdAsync(userId, id, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateTodoItemRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _todoItemService.UpdateAsync(userId, id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _todoItemService.CompleteAsync(userId, id, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await _todoItemService.DeleteAsync(userId, id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/permanent")]
    public async Task<IActionResult> PermanentDelete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await _todoItemService.PermanentDeleteAsync(userId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _todoItemService.RestoreAsync(userId, id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("trash")]
    public async Task<IActionResult> GetTrash([FromQuery] PaginatedRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _todoItemService.GetTrashAsync(userId, request, cancellationToken);
        return Ok(result);
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

    [HttpGet("{id}/activities")]
    public async Task<IActionResult> GetActivities(Guid id)
    {
        var userId = GetCurrentUserId();

        var _activityService = HttpContext.RequestServices.GetRequiredService<ITodoItemActivityService>();
        var activities = await _activityService.GetActivitiesByTaskIdAsync(id, userId);

        return Ok(new CollectionResponse<TodoItemActivityResponse>(activities));
    }
}

