using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.DTOs.Vote;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/games/{gameId:guid}/issues/{issueId:guid}")]
public class VotesController : ControllerBase
{
    private readonly IVoteService _voteService;
    private readonly IRoomStateService _roomService;
    public VotesController(IVoteService votesService, IRoomStateService roomService)
    {
        _voteService = votesService;
        _roomService = roomService;
    }

    [HttpPost("votes/me")]
    public async Task<IActionResult> CreateVote([FromRoute] Guid gameId, [FromRoute] Guid issueId, [FromBody] CreateVoteDto dto)
    {
        var  vote = await _voteService.CreateVoteByGameIdAsync(gameId, issueId, dto);
        return Ok(vote);
    }

    [HttpDelete("votes/me")]
    public async Task<IActionResult> DeleteVote([FromRoute] Guid gameId, [FromRoute] Guid issueId)
    {
        await _voteService.DeleteVoteAsync(gameId, issueId);
        return NoContent();
    }

    [HttpGet("final-estimate")]
    public async Task<IActionResult> GetFinalEstimate([FromRoute] Guid gameId, [FromRoute]Guid issueId)
    {
        var state = await _roomService.GetFinalEstimateAsync(gameId, issueId);
        return Ok(state);
    }
}



