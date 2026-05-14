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

    public TimerController(ITimerService timerService)
    {
        _timerService = timerService;

    }

    [HttpPost("timer-start")]
    public async Task<IActionResult> Start([FromRoute] Guid gameId, [FromBody] StartTimerRequestDto request)
    {
        var result = await _timerService.StartTimerAsync(gameId, request.DurationSeconds);
        return Ok(result);
    }

    [HttpPost("timer-stop")]
    public async Task<IActionResult> Stop([FromRoute] Guid gameId)
    {
        await _timerService.StopTimerAsync(gameId);
        return NoContent();
    }
}
