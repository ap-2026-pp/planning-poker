using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.API.Services;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.DTOs.Participant;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class GameController(
    IGameService service,
    IParticipantService participantService,
    InviteLinkService inviteLinkService) : ControllerBase
{
   
    [HttpPost]
    public async Task<ActionResult<GameDto>> CreateGame([FromBody] CreateGameRequestDto createCreateGameRequestDto)
    {
        var createdGame = await service.AddGameAsync(createCreateGameRequestDto);
        return Ok(createdGame);
    }

    [HttpGet("{gameId:guid}")]
    public async Task<ActionResult<GameDto>> GetGame(Guid gameId)
    {
        var game = await service.GetGameByIdAsync(gameId);
        return Ok(game);
    }

    [HttpGet("{gameId:guid}/invite")]
    public async Task<ActionResult<GameInviteDto>> GetGameInvite(Guid gameId)
    {
        var game = await service.GetGameInviteAsync(gameId);
        return Ok(inviteLinkService.BuildInviteDto(game.Id, game.InviteCode, Request));
    }

    [HttpPost("join/{inviteCode}")]
    public async Task<ActionResult<GameDto>> JoinGameByInviteCode(string inviteCode, 
        [FromBody] JoinGameRequestDto joinGameRequestDto)
    {
        var game = await participantService.JoinGameByInviteCodeAsync(inviteCode, joinGameRequestDto.DisplayName);
        return Ok(game);
    }

    [HttpPost("{gameId:guid}/leave")]
    public async Task<ActionResult> LeaveGame(Guid gameId)
    {
        await participantService.LeaveGameAsync(gameId);
        return NoContent();
    }

    [HttpPut("{gameId:guid}")]
    public async Task<ActionResult<GameDto>> UpdateGame(Guid gameId, [FromBody] UpdateGameRequestDto updateGameRequestDto)
    {
        var updatedGame = await service.UpdateGameAsync(gameId, updateGameRequestDto);
        return Ok(updatedGame);
    }

    [HttpDelete("{gameId:guid}")]
    public async Task<ActionResult> DeleteGame(Guid gameId)
    {
        await service.DeleteGameAsync(gameId);
        return NoContent();
    }

    [HttpPut("{gameId:guid}/change-display-name")]
    public async Task<ActionResult> ChangeDisplayName(Guid gameId, [FromBody] UpdateDisplayNameDto updateDisplayNameDto)
    {
        var updatedParticipant = await participantService.UpdateDisplayNameAsync(gameId, updateDisplayNameDto.DisplayName);
        return Ok(ParticipantMapper.ToGameParticipantDto(updatedParticipant));
    }
}
