using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IGameService
{
    public Task<GameDto> AddGameAsync(CreateGameRequestDto game);
    public Task<GameDto> GetGameByIdAsync(Guid gameId); 
    public Task<GameDto> UpdateGameAsync(Guid gameId, UpdateGameRequestDto game);
    public Task<Game> GetGameInviteAsync(Guid gameId);
    public Task DeleteGameAsync(Guid gameId);
    public Task<IEnumerable<GameDto>?> GetUserGamesAsync(UserGamesScope scope);
}
