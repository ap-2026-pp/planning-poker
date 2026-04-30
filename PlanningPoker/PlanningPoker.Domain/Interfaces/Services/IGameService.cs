using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IGameService
{
    public Task<GameDto> AddGameAsync(Guid userId, CreateGameRequestDto game);
    public Task<GameDto> GetGameByIdAsync(Guid gameId); 
    public Task<GameDto> UpdateGameAsync(Guid gameId, Guid userId, UpdateGameRequestDto game);
    public Task<Game> GetGameInviteAsync(Guid gameId, Guid userId);
    public Task DeleteGameAsync(Guid gameId, Guid userId);
}
