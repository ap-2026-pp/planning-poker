using PlanningPoker.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.DTOs.Timer;

namespace PlanningPoker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/games/{gameId:guid}/timer")]
public class TimerController : ControllerBase
{
    private readonly ITimerService _timerService;
    private readonly IRoomStateService _roomStateService;
    private readonly IGameRealtimeService _gameRealtimeService;

    public TimerController(
        ITimerService timerService,
        IRoomStateService roomStateService,
        IGameRealtimeService gameRealtimeService)
    {
        _timerService = timerService;
        _roomStateService = roomStateService;
        _gameRealtimeService = gameRealtimeService;
    }

    [HttpPost("timer-start")]
    public async Task<IActionResult> Start([FromRoute] Guid gameId, [FromBody] StartTimerRequestDto request)
    {
        var result = await _timerService.StartTimerAsync(gameId, request.DurationSeconds);
        var roomState = await _roomStateService.GetRoomStateAsync(gameId);
        await _gameRealtimeService.NotifyRoundStateUpdatedAsync(gameId, roomState);
        return Ok(result);
    }

    [HttpPost("timer-stop")]
    public async Task<IActionResult> Stop([FromRoute] Guid gameId)
    {
        await _timerService.StopTimerAsync(gameId);
        var roomState = await _roomStateService.GetRoomStateAsync(gameId);
        await _gameRealtimeService.NotifyRoundStateUpdatedAsync(gameId, roomState);
        return NoContent();
    }
}
