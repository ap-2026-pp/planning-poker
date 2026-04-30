using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.API.Services;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class GameController(
    IGameService gameService,
    InviteLinkService inviteLinkService,
    ICurrentUserAccessor currentUserAccessor) : ControllerBase
{
   
    [HttpPost]
    public async Task<ActionResult<GameDto>> CreateGame([FromBody] CreateGameRequestDto createGameRequestDto)
    {
        var createdGame = await gameService.AddGameAsync(currentUserAccessor.GetRequiredUserId(), createGameRequestDto);
        return Ok(createdGame);
    }

    [HttpGet("{gameId:guid}")]
    public async Task<ActionResult<GameDto>> GetGame(Guid gameId)
    {
        var game = await gameService.GetGameByIdAsync(gameId);
        return Ok(game);
    }

    [HttpGet("{gameId:guid}/invite")]
    public async Task<ActionResult<GameInviteDto>> GetGameInvite(Guid gameId)
    {
        var game = await gameService.GetGameInviteAsync(gameId, currentUserAccessor.GetRequiredUserId());
        return Ok(inviteLinkService.BuildInviteDto(game.Id, game.InviteCode, Request));
    }

    [HttpPut("{gameId:guid}")]
    public async Task<ActionResult<GameDto>> UpdateGame(Guid gameId, [FromBody] UpdateGameRequestDto updateGameRequestDto)
    {
        var updatedGame = await gameService.UpdateGameAsync(gameId, currentUserAccessor.GetRequiredUserId(), updateGameRequestDto);
        return Ok(updatedGame);
    }

    [HttpDelete("{gameId:guid}")]
    public async Task<ActionResult> DeleteGame(Guid gameId)
    {
        await gameService.DeleteGameAsync(gameId, currentUserAccessor.GetRequiredUserId());
        return NoContent();
    }
}
