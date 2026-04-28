using System.Security.Cryptography;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

public class GameService(
    IGameRepository gameRepository,
    ICurrentUserService currentUserService) : IGameService
{
    private const int InviteCodeLength = 20;
    private const string InviteCodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    
    public async Task<GameDto> AddGameAsync(CreateGameRequestDto createGameRequestDto)
    {
        var currentUserId = currentUserService.GetRequiredUserId();

        var game = GameMapper.ToGame(createGameRequestDto);
        await EnsureUniqueGameNameAsync(game.Name, currentUserId);
        
        game.CreatedBy = currentUserId;
        game.InviteCode = await GenerateInviteCodeAsync();
        game.Participants = [CreateMasterParticipant(currentUserId, createGameRequestDto.HostDisplayName)];
        
        await gameRepository.AddAsync(game);
        await gameRepository.SaveChangesAsync();
        
        return GameMapper.ToGameDto(game);
    }

    public async Task<GameDto> GetGameByIdAsync(Guid gameId)
    {
        var game = await GetGameOrThrowAsync(gameId);
        return GameMapper.ToGameDto(game);
    }

    public async Task<GameDto> UpdateGameAsync(Guid gameId, UpdateGameRequestDto updateGameRequestDto)
    {
        var currentUserId = currentUserService.GetRequiredUserId();
        var existingGame = await GetGameOrThrowAsync(gameId);
        EnsureUserIsGameMaster(existingGame, currentUserId, "update");

        var updatedGame = GameMapper.ToGame(updateGameRequestDto);
        await EnsureUniqueGameNameAsync(updatedGame.Name, existingGame.CreatedBy, gameId);
        ApplyGameUpdates(existingGame, updatedGame);
        
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
        
        EnsureUserIsGameMaster(game, currentUserId, "delete");
        game.IsActive = false;
        game.IsDeleted = true;
        gameRepository.Update(game);
        await gameRepository.SaveChangesAsync();
    }

    private async Task<string> GenerateInviteCodeAsync()
    {
        var inviteCodeBuffer = new char[InviteCodeLength];
        string inviteCode;

        do
        {
            for (var i = 0; i < inviteCodeBuffer.Length; i++)
            {
                inviteCodeBuffer[i] = InviteCodeAlphabet[RandomNumberGenerator.GetInt32(InviteCodeAlphabet.Length)];
            }

            inviteCode = new string(inviteCodeBuffer);
        } while (await gameRepository.ExistsByInviteCodeAsync(inviteCode));

        return inviteCode;
    }
    
    public async Task<Game> GetGameInviteAsync(Guid gameId)
    {
        var currentUserId = currentUserService.GetRequiredUserId();
        var game = await GetGameOrThrowAsync(gameId);
        EnsureUserIsParticipant(game, currentUserId, "view", "game invite");

        return game;
    }

    private async Task<Game> GetGameOrThrowAsync(Guid gameId)
    {
        return await gameRepository.GetByIdAsync(gameId)
               ?? throw new NotFoundException(nameof(Game), gameId);
    }

    private async Task EnsureUniqueGameNameAsync(string gameName, Guid createdBy, Guid? excludedGameId = null)
    {
        var exists = excludedGameId.HasValue
            ? await gameRepository.ExistsByNameAsync(gameName, createdBy, excludedGameId.Value)
            : await gameRepository.ExistsByNameAsync(gameName, createdBy);

        if (exists)
        {
            throw new ResourceAlreadyExistsException(nameof(Game), gameName);
        }
    }

    private static GameParticipant CreateMasterParticipant(Guid currentUserId, string displayName)
    {
        return new GameParticipant
        {
            Id = Guid.NewGuid(),
            UserId = currentUserId,
            DisplayName = displayName,
            Role = ParticipantRole.Master,
            JoinedAt = DateTime.UtcNow,
            IsConnected = true
        };
    }

    private static void ApplyGameUpdates(Game existingGame, Game updatedGame)
    {
        existingGame.Name = updatedGame.Name;
        existingGame.AutoRevealCards = updatedGame.AutoRevealCards;
        existingGame.IsActive = updatedGame.IsActive;
        existingGame.ShowAverage = updatedGame.ShowAverage;
        existingGame.VotingSystem = updatedGame.VotingSystem;
        existingGame.ShowCountdownAnimation = updatedGame.ShowCountdownAnimation;
    }

    private static void EnsureUserIsGameMaster(Game game, Guid currentUserId, string action)
    {
        var isGameMaster = game.Participants.Any(participant =>
            participant.UserId == currentUserId &&
            participant.Role == ParticipantRole.Master);

        if (!isGameMaster)
        {
            throw new ForbiddenException(action, "game");
        }
    }

    private static void EnsureUserIsParticipant(Game game, Guid currentUserId, string action, string resourceName)
    {
        var isParticipant = game.Participants.Any(participant => participant.UserId == currentUserId);
        if (!isParticipant)
        {
            throw new ForbiddenException(action, resourceName);
        }
    }
}
