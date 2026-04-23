using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IGameService
{
    public Task<Game> AddGameAsync(Game game);
}