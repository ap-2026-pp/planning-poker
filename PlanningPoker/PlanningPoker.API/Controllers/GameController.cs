using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;

namespace PlanningPoker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController(IGameService service) : ControllerBase
{
   
    [HttpPost]
    public async Task<ActionResult> CreateGame([FromBody] GameDto createGameDto)
    {
        var game = GameMapper.ToGame(createGameDto);
        var createdGame = await service.AddGameAsync(game);
       
        return Ok(GameMapper.ToGameDto(createdGame));
    }

    [HttpGet("{gameId:guid}")]
    public async Task<ActionResult<GameDto>> GetGame(Guid gameId)
    {
        var game = await service.GetGameByIdAsync(gameId);
        return Ok(GameMapper.ToGameRequestDto(game));
    }

    [HttpPut("{gameId:guid}")]
    public async Task<ActionResult> UpdateGame(Guid gameId, [FromBody] GameDto updateGameDto)
    {
        var game = GameMapper.ToGame(updateGameDto);
        var updatedGame = await service.UpdateGameAsync(gameId, game);
        return Ok(GameMapper.ToGameDto(updatedGame));
    }

    [HttpDelete("{gameId:guid}")]
    public async Task<ActionResult> DeleteGame(Guid gameId)
    {
        await service.DeleteGameAsync(gameId);
        return NoContent();
    }
}