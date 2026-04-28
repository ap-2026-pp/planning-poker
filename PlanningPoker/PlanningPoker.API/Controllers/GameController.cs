using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;

namespace PlanningPoker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class GameController(IGameService service) : ControllerBase
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
}
