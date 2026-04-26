using System.Security.Claims;
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
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        var game = GameMapper.ToGame(createCreateGameRequestDto);
        var createdGame = await service.AddGameAsync(currentUserId.Value, game);
       
        return Ok(GameMapper.ToGameDto(createdGame));
    }

    [HttpGet("{gameId:guid}")]
    public async Task<ActionResult<GameDto>> GetGame(Guid gameId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        var game = await service.GetGameByIdAsync(gameId);
        return Ok(GameMapper.ToGameDto(game));
    }

    [HttpPut("{gameId:guid}")]
    public async Task<ActionResult<GameDto>> UpdateGame(Guid gameId, [FromBody] UpdateGameRequestDto updateGameRequestDto)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        var game = GameMapper.ToGame(updateGameRequestDto);
        var updatedGame = await service.UpdateGameAsync(gameId, game, currentUserId.Value);
        return Ok(GameMapper.ToGameDto(updatedGame));
    }

    [HttpDelete("{gameId:guid}")]
    public async Task<ActionResult> DeleteGame(Guid gameId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        await service.DeleteGameAsync(gameId, currentUserId.Value);
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
