using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

public class GameService(IGameRepository gameRepository, IUserRepository userRepository) : IGameService
{
    public async Task<Game> AddGameAsync(Guid currentUserId, Game game)
    {
        var user = await userRepository.GetByIdAsync(currentUserId);
        if (user is null)
        {
            throw new NotFoundException(currentUserId);
        }

        var exists = await gameRepository.ExistsByNameAsync(game.Name, currentUserId);
        if (exists)
        {
            throw new GameAlreadyExistsException(game.Name); 
        }
        
        game.CreatedBy = currentUserId;
        game.IsActive = true;
        game.InviteCode = "123"; //TODO autogeneration etc
        game.Participants = new List<GameParticipant>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DisplayName = user.DisplayName,
                Role = Role.Master,
                JoinedAt = DateTime.UtcNow,
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

    public async Task<Game> UpdateGameAsync(Guid gameId, Game game, Guid currentUserId)
    {
        var existingGame = await gameRepository.GetByIdAsync(gameId);
        if (existingGame is null)
        {
            throw new NotFoundException(gameId);
        }

        if (currentUserId != existingGame.CreatedBy)
        {
            throw new UnauthorizedAccessException("Forbidden"); // TODO forbidden exception
        }

        var exists = await gameRepository.ExistsByNameAsync(game.Name, existingGame.CreatedBy, gameId);
        if (exists)
        {
            throw new GameAlreadyExistsException(game.Name);
        }
        
        existingGame.Name = game.Name;
        existingGame.AutoRevealCards = game.AutoRevealCards;
        existingGame.IsActive = game.IsActive;
        existingGame.ShowAverage = game.ShowAverage;
        existingGame.VotingSystem = game.VotingSystem;
        existingGame.ShowCountdownAnimation = game.ShowCountdownAnimation;
        
        gameRepository.Update(existingGame);
        await gameRepository.SaveChangesAsync();
        return existingGame;
    }

    public async Task DeleteGameAsync(Guid gameId, Guid currentUserId)
    {
        var game = await gameRepository.GetByIdAsync(gameId);
        
        if (game is null)
        {
            return;
        }
        
        if (currentUserId != game.CreatedBy)
        {
            throw new UnauthorizedAccessException("Forbidden"); // TODO forbidden exception
        }
        
        game.IsActive = false;
        game.IsDeleted = true;
        gameRepository.Update(game);
        await gameRepository.SaveChangesAsync();
    }
}
