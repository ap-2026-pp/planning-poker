using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.DTOs.Participant;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/games")]
public class GameParticipantController(
    IParticipantService participantService, 
    IGameAccessService gameAccessService) : ControllerBase
{
    [HttpGet("{gameId:guid}/participants")]
    public async Task<ActionResult<IEnumerable<GameParticipantDto>>> GetGameParticipants(Guid gameId)
    {
        return Ok(await participantService.GetGameParticipantsAsync(gameId));
    }

    [HttpDelete("{gameId:guid}/parЙticipants/{participantId:guid}")]
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

    [HttpPatch("{gameId:guid}/participants/me/spectator")]
    public async Task<ActionResult> SetSpectatorMode(Guid gameId, [FromBody] bool isSpectator)
    {
        await participantService.SetSpectatorModeAsync(gameId, isSpectator);
        return NoContent();
    }
    
    [HttpPatch("{gameId:guid}/participants/{participantId:guid}/transfer-master")]
    public async Task<ActionResult> TransferMaster(Guid gameId, Guid participantId)
    {
        await participantService.TransferMasterAsync(gameId, participantId);
        return NoContent();
    }

    [HttpPut("{gameId:guid}/participants/{participantId}/permissions")]
    public async Task<IActionResult> UpdatePermissions(Guid gameId, [FromBody] UpdateBulkPermissionsRequestDto dto)
    {
        await gameAccessService.UpdateBulkPermissionsAsync(gameId, dto);
        return Ok();  
    }
}
