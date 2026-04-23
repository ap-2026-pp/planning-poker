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

    public async Task<Game> GetGameByIdAsync(Guid gameId)
    {
        var game = await gameRepository.GetByIdAsync(gameId);
        return game ?? throw new NotFoundException(gameId);
    }

    public async Task<Game> UpdateGameAsync(Guid gameId, Game game)
    {
        var existingGame = await gameRepository.GetByIdAsync(gameId);
        if (existingGame is null)
        {
            throw new NotFoundException(gameId);
        }
        
        existingGame.Name = game.Name;
        existingGame.InviteCode = "123";
        existingGame.Participants = game.Participants;
        existingGame.AutoRevealCards = game.AutoRevealCards;
        existingGame.IsActive = game.IsActive;
        existingGame.ShowAverage = game.ShowAverage;
        existingGame.VotingSystem = game.VotingSystem;
        existingGame.ShowCountdownAnimation = game.ShowCountdownAnimation;
        existingGame.CreatedBy = game.CreatedBy;
        
        gameRepository.Update(game);
        await gameRepository.SaveChangesAsync();
        return  game;
    }

    public async Task DeleteGameAsync(Guid gameId)
    {
        var game = await gameRepository.GetByIdAsync(gameId);
        
        if (game is null)
        {
            return;
        }
        
        game.IsActive = false;
        game.IsDeleted = true;
        gameRepository.Update(game);
        await gameRepository.SaveChangesAsync();
    }
}
