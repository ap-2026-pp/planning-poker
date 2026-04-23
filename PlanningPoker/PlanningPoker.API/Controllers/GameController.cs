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
    public async Task<ActionResult> CreateGame([FromBody] GameDto gameDto)
    {
        var game = GameMapper.ToGame(gameDto);
        var createdGame = await service.AddGameAsync(game);
       
        return Ok(GameMapper.ToGameDto(createdGame));
    }
}