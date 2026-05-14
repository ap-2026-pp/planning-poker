using PlanningPoker.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<IActionResult> Start([FromRoute] Guid gameId)
    {
        var result = await _timerService.StartTimerAsync(gameId);
        return Ok(result);
    }

    [HttpPost("timer-stop")]
    public async Task<IActionResult> Stop([FromRoute] Guid gameId)
    {
        await _timerService.StopTimerAsync(gameId);
        return NoContent();
    }
}