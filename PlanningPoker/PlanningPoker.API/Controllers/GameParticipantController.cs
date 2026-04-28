using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;

namespace PlanningPoker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/game/{gameId:guid}/participants")]
public class GameParticipantController(IParticipantService service) : ControllerBase
{
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GameParticipantDto>>> GetGameParticipants(Guid gameId)
    {
        return Ok(await service.GetGameParticipantsAsync(gameId));
    }

    [HttpDelete("{participantId:guid}")]
    public async Task<ActionResult> DeleteGameParticipant([FromRoute]Guid gameId, Guid participantId)
    {
        await service.DeleteGameParticipantAsync(gameId, participantId);
        return NoContent();
    }
}
