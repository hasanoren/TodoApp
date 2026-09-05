using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;

namespace TodoApp.Api.Controllers;

[Authorize]
[ApiController]
public class TransferRequestsController : ControllerBase
{
    private readonly ITaskTransferService _taskTransferService;

    public TransferRequestsController(ITaskTransferService taskTransferService)
    {
        _taskTransferService = taskTransferService;
    }

    // POST /api/todoitems/{taskId}/transfer-requests — Devir talebi başlatma
    [HttpPost("api/todoitems/{taskId:guid}/transfer-requests")]
    public async Task<IActionResult> CreateTransferRequest(Guid taskId, CreateTransferRequestDto dto)
    {
        var currentOwnerId = GetCurrentUserId();
        var result = await _taskTransferService.CreateTransferRequestAsync(currentOwnerId, taskId, dto);
        return Ok(result);
    }

    // GET /api/transfer-requests/pending — Kullanıcının onayını bekleyen devir talepleri
    [HttpGet("api/transfer-requests/pending")]
    public async Task<IActionResult> GetPendingRequests()
    {
        var currentUserId = GetCurrentUserId();
        var result = await _taskTransferService.GetPendingRequestsAsync(currentUserId);
        return Ok(result);
    }

    // POST /api/transfer-requests/{requestId}/accept — Devir talebini kabul etme
    [HttpPost("api/transfer-requests/{requestId:guid}/accept")]
    public async Task<IActionResult> AcceptTransferRequest(Guid requestId)
    {
        var targetUserId = GetCurrentUserId();
        await _taskTransferService.AcceptTransferRequestAsync(targetUserId, requestId);
        return Ok(new { message = "Görev devir talebi başarıyla kabul edildi ve sahiplik aktarıldı." });
    }

    // POST /api/transfer-requests/{requestId}/reject — Devir talebini reddetme
    [HttpPost("api/transfer-requests/{requestId:guid}/reject")]
    public async Task<IActionResult> RejectTransferRequest(Guid requestId)
    {
        var targetUserId = GetCurrentUserId();
        await _taskTransferService.RejectTransferRequestAsync(targetUserId, requestId);
        return Ok(new { message = "Görev devir talebi reddedildi." });
    }

    // POST /api/transfer-requests/{requestId}/cancel — Devir talebini iptal etme (sahip tarafından)
    [HttpPost("api/transfer-requests/{requestId:guid}/cancel")]
    public async Task<IActionResult> CancelTransferRequest(Guid requestId)
    {
        var ownerUserId = GetCurrentUserId();
        await _taskTransferService.CancelTransferRequestAsync(ownerUserId, requestId);
        return Ok(new { message = "Görev devir talebi başarıyla iptal edildi." });
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

