using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;

namespace PlanningPoker.API.Controllers;

[ApiController]
[Route("api/Game/{gameId:guid}/Participants")]
public class GameParticipantController(IParticipantService service) : ControllerBase
{
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GameParticipantDto>>> GetGameParticipants(Guid gameId)
    {
        var participants = await service.GetGameParticipantsAsync(gameId);
        return Ok(participants.Select(ParticipantMapper.ToGameParticipantDto));
    }

    [HttpDelete("{participantId:guid}")]
    public async Task<ActionResult> DeleteGameParticipant([FromRoute]Guid gameId, Guid participantId)
    {
        await service.DeleteGameParticipantAsync(gameId, participantId);
        return NoContent();
    }
}