using PlanningPoker.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PlanningPoker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/games/{gameId:guid}/room")]
public class RoomController : ControllerBase
{
    private readonly IRoomStateService _roomService;
    public RoomController(IRoomStateService roomService)
    {
        _roomService = roomService;
    }

    [HttpGet("round")]
    public async Task<IActionResult> GetRoomState([FromRoute] Guid gameId)
    {
        var state = await _roomService.GetRoomStateAsync(gameId);
        return Ok(state);
    }

    [HttpGet("reveal")]
     public async Task<IActionResult> RevealCards([FromRoute] Guid gameId)
    {
        var state = await _roomService.RevealCardsAsync(gameId);
        return Ok(state);
    }

    [HttpPost("issues/{issueId:guid}/reset-round")]
    public async Task<IActionResult> ResetIssueRound(
        [FromRoute] Guid gameId,
        [FromRoute] Guid issueId)
    {
        var state = await _roomService.ResetRoundAsync(gameId, issueId);
        return Ok(state);
    }
}