using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IGameService
{
    public Task<Game> AddGameAsync(Guid currentUserId, Game game);
    public Task<Game> GetGameByIdAsync(Guid gameId); 
    public Task<Game> UpdateGameAsync(Guid gameId, Game game, Guid currentUserId);
    public Task DeleteGameAsync(Guid gameId, Guid currentUserId);
}
