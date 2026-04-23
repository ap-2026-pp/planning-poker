using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

public class GameService(IGameRepository gameRepository, IUserRepository userRepository) : IGameService
{
    public async Task<Game> AddGameAsync(Game game)
    {
        var user = await userRepository.GetByIdAsync(game.CreatedBy);
        if (user is null) 
        {
            throw new NotFoundException(game.CreatedBy);
        }

        var exists = await gameRepository.ExistsByNameAsync(game.Name, game.CreatedBy);
        if (exists)
        {
            throw new GameAlreadyExistsException(game.Name); 
        }

        game.InviteCode = "123"; //TODO autogeneration etc
        game.Participants = new List<GameParticipant>
        {
            new()
            {
                UserId = user.Id,
                DisplayName = user.DisplayName,
                Role = Role.Master,
                IsConnected = true
            }
        };
        
        await gameRepository.AddAsync(game);
        await gameRepository.SaveChangesAsync();
        return game;
    }
}
