using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IGameService
{
    public Task<Game> AddGameAsync(Game game);
    public Task<Game> GetGameByIdAsync(Guid gameId); 
    public Task<Game> UpdateGameAsync(Guid gameId, Game game);
    public Task DeleteGameAsync(Guid gameId);
}