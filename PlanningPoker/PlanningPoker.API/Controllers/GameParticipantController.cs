using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.DTOs.Participant;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/games")]
public class GameParticipantController(IParticipantService participantService) : ControllerBase
{
    [HttpGet("{gameId:guid}/participants")]
    public async Task<ActionResult<IEnumerable<GameParticipantDto>>> GetGameParticipants(Guid gameId)
    {
        return Ok(await participantService.GetGameParticipantsAsync(gameId));
    }

    [HttpDelete("{gameId:guid}/participants/{participantId:guid}")]
    public async Task<ActionResult> DeleteGameParticipant(Guid gameId, Guid participantId)
    {
        await participantService.DeleteGameParticipantAsync(gameId, participantId);
        return NoContent();
    }

    [HttpPost("join/{inviteCode}")]
    public async Task<ActionResult<GameDto>> JoinGameByInviteCode(
        string inviteCode,
        [FromBody] JoinGameRequestDto joinGameRequestDto)
    {
        var game = await participantService.JoinGameByInviteCodeAsync(inviteCode, joinGameRequestDto.DisplayName);
        return Ok(game);
    }

    [HttpDelete("{gameId:guid}/participants/me")]
    public async Task<ActionResult> LeaveGame(Guid gameId)
    {
        await participantService.LeaveGameAsync(gameId);
        return NoContent();
    }

    [HttpPut("{gameId:guid}/participants/me/display-name")]
    public async Task<ActionResult<GameParticipantDto>> ChangeDisplayName(
        Guid gameId,
        [FromBody] UpdateDisplayNameDto updateDisplayNameDto)
    {
        var updatedParticipant = await participantService.UpdateDisplayNameAsync(gameId, updateDisplayNameDto.DisplayName);
        return Ok(updatedParticipant);
    }
}
