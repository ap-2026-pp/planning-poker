using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

public class GameService(
    IGameRepository gameRepository,
    IUserRepository userRepository,
    ICurrentUserService currentUserService) : IGameService
{
    public async Task<GameDto> AddGameAsync(CreateGameRequestDto createGameRequestDto)
    {
        var game = GameMapper.ToGame(createGameRequestDto);
        
        var currentUserId = currentUserService.GetRequiredUserId();
        var user = await userRepository.GetByIdAsync(currentUserId);
        if (user is null)
        {
            throw new NotFoundException(nameof(User), currentUserId);
        }

        var exists = await gameRepository.ExistsByNameAsync(game.Name, currentUserId);
        if (exists)
        {
            throw new GameAlreadyExistsException(game.Name); 
        }
        
        game.CreatedBy = currentUserId;
        game.IsActive = true;
        game.InviteCode = "123"; 
        game.Participants = new List<GameParticipant>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DisplayName = user.DisplayName,
                Role = ParticipantRole.Master,
                JoinedAt = DateTime.UtcNow,
                IsConnected = true
            }
        };
        
        await gameRepository.AddAsync(game);
        await gameRepository.SaveChangesAsync();
        
        return GameMapper.ToGameDto(game);
    }

    public async Task<GameDto> GetGameByIdAsync(Guid gameId)
    {
        var game = await gameRepository.GetByIdAsync(gameId);
        return GameMapper.ToGameDto(game ?? throw new NotFoundException(nameof(Game), gameId));
    }

    public async Task<GameDto> UpdateGameAsync(Guid gameId, UpdateGameRequestDto updateGameRequestDto)
    {
        var game = GameMapper.ToGame(updateGameRequestDto);
        
        var currentUserId = currentUserService.GetRequiredUserId();
        var existingGame = await gameRepository.GetByIdAsync(gameId);
        if (existingGame is null || existingGame.IsDeleted)
        {
            throw new NotFoundException(nameof(Game), gameId);
        }

        if (!IsMaster(existingGame, currentUserId))
        {
            throw new ForbiddenException("update", "game");
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
        
        return GameMapper.ToGameDto(existingGame);
    }

    public async Task DeleteGameAsync(Guid gameId)
    {
        var currentUserId = currentUserService.GetRequiredUserId();
        var game = await gameRepository.GetByIdAsync(gameId);
        
        if (game is null)
        {
            return;
        }
        
        if (!IsMaster(game, currentUserId))
        {
            throw new ForbiddenException("delete", "game");
        }
        
        game.IsActive = false;
        game.IsDeleted = true;
        gameRepository.Update(game);
        await gameRepository.SaveChangesAsync();
    }

    private static bool IsMaster(Game game, Guid currentUserId)
    {
        return game.Participants.Any(participant =>
            participant.UserId == currentUserId &&
            participant.Role == ParticipantRole.Master);
    }
}
