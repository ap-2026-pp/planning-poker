using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;

namespace PlanningPoker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/Game/{gameId:guid}/Participants")]
public class GameParticipantController(IParticipantService service) : ControllerBase
{
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GameParticipantDto>>> GetGameParticipants(Guid gameId)
    {
        var participants = await service.GetGameParticipantsAsync(gameId);
        return Ok((participants ?? []).Select(ParticipantMapper.ToGameParticipantDto));
    }

    [HttpDelete("{participantId:guid}")]
    public async Task<ActionResult> DeleteGameParticipant([FromRoute]Guid gameId, Guid participantId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }
        
        await service.DeleteGameParticipantAsync(currentUserId.Value, gameId, participantId);
        return NoContent();
    }
    
    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        return userId;
    }
}