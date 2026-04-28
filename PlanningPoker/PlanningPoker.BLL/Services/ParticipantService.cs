using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

public class ParticipantService(
    IParticipantRepository participantRepository, IGameRepository gameRepository,
    ICurrentUserService currentUserService) : IParticipantService
{
    public async Task<IEnumerable<GameParticipantDto>> GetGameParticipantsAsync(Guid gameId)
    {
        var participants = await participantRepository.GetGameParticipantsAsync(gameId);
        return (participants ?? []).Select(ParticipantMapper.ToGameParticipantDto);
    }
    
    public async Task<GameDto> JoinGameByInviteCodeAsync(string inviteCode, string displayName)
    {
        var currentUserId = currentUserService.GetRequiredUserId();
        var game = await GetActiveGameByInviteCodeOrThrowAsync(inviteCode);
        await EnsureDisplayNameIsAvailableAsync(displayName, game.Id);

        var activeParticipant = await participantRepository.GetByUserIdAndGameIdAsync(currentUserId, game.Id);
        if (activeParticipant is not null)
        {
            activeParticipant.IsConnected = true;
            activeParticipant.JoinedAt = DateTime.UtcNow;
            await participantRepository.SaveChangesAsync();
            return await GetGameDtoAsync(game);
        }

        var existingParticipant = await participantRepository.GetByUserIdAndGameIdIncludingRemovedAsync(currentUserId, game.Id);
        if (existingParticipant is not null)
        {
            existingParticipant.IsConnected = true;
            existingParticipant.JoinedAt = DateTime.UtcNow;
            existingParticipant.RemovedAt = null;
            participantRepository.Update(existingParticipant);
        }
        else
        {
            await participantRepository.AddAsync(new GameParticipant
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                UserId = currentUserId,
                DisplayName = displayName,
                Role = ParticipantRole.Player,
                JoinedAt = DateTime.UtcNow,
                IsConnected = true
            });
        }

        await participantRepository.SaveChangesAsync();
        return await GetGameDtoAsync(game);
    }

    public async Task LeaveGameAsync(Guid gameId)
    {
        var currentUserId = currentUserService.GetRequiredUserId();
        var participant = await participantRepository.GetByUserIdAndGameIdAsync(currentUserId, gameId);
        if (participant is null)
        {
            throw new NotFoundException("Active game participant was not found.");
        }

        if (participant.Role == ParticipantRole.Master)
        {
            throw new ForbiddenException("The game master cannot leave the game. Delete the game or transfer ownership first.");
        }

        participantRepository.RemoveGameParticipant(participant);
        await participantRepository.SaveChangesAsync();
    }

    public async Task DeleteGameParticipantAsync(Guid gameId, Guid participantId)
    {
        var currentUserId = currentUserService.GetRequiredUserId();
        await GetGameOrThrowAsync(gameId);
        await EnsureCurrentUserIsGameMasterAsync(currentUserId, gameId);
        
        var participant = await GetParticipantInGameOrThrowAsync(gameId, participantId);
        participantRepository.RemoveGameParticipant(participant);
        await participantRepository.SaveChangesAsync();
    }

    public async Task<GameParticipantDto> UpdateDisplayNameAsync(Guid gameId, string displayName)
    {
        var currentUserId = currentUserService.GetRequiredUserId();
        var game = await GetGameOrThrowAsync(gameId);
        await EnsureDisplayNameIsAvailableAsync(displayName, game.Id);

        var currentUserParticipant = await GetCurrentUserParticipantOrThrowAsync(currentUserId, gameId);
        currentUserParticipant.DisplayName = displayName;
        await participantRepository.SaveChangesAsync();
        return ParticipantMapper.ToGameParticipantDto(currentUserParticipant);
    }

    private async Task<Game> GetActiveGameByInviteCodeOrThrowAsync(string inviteCode)
    {
        var game = await gameRepository.GetByInviteCodeAsync(inviteCode)
                   ?? throw new NotFoundException(nameof(Game), nameof(Game.InviteCode), inviteCode);

        if (!game.IsActive)
        {
            throw new ForbiddenException("join", "game");
        }
        
        return game;
    }

    private async Task<Game> GetGameOrThrowAsync(Guid gameId)
    {
        return await gameRepository.GetByIdAsync(gameId)
               ?? throw new NotFoundException(nameof(Game), gameId);
    }

    private async Task EnsureDisplayNameIsAvailableAsync(string displayName, Guid gameId)
    {
        var exists = await participantRepository.ExistsByDisplayNameAsync(displayName, gameId);
        if (exists)
        {
            throw new ResourceAlreadyExistsException(nameof(GameParticipant), displayName); // TODO change exception
        }
    }
    
    private async Task EnsureCurrentUserIsGameMasterAsync(Guid currentUserId, Guid gameId)
    {
        var currentUserParticipant = await participantRepository.GetByUserIdAndGameIdAsync(currentUserId, gameId);
        if (currentUserParticipant is null || currentUserParticipant.Role != ParticipantRole.Master)
        {
            throw new ForbiddenException("delete", "participant");
        }
    }

    private async Task<GameParticipant> GetParticipantInGameOrThrowAsync(Guid gameId, Guid participantId)
    {
        var participant = await participantRepository.GetActiveByIdAsync(participantId);
        if (participant is null || participant.GameId != gameId)
        {
            throw new NotFoundException(nameof(GameParticipant), participantId);
        }

        return participant;
    }

    private async Task<GameParticipant> GetCurrentUserParticipantOrThrowAsync(Guid currentUserId, Guid gameId)
    {
        return await participantRepository.GetByUserIdAndGameIdAsync(currentUserId, gameId)
               ?? throw new NotFoundException("User participant was not found in the game.");
    }

    private async Task<GameDto> GetGameDtoAsync(Game fallbackGame)
    {
        var game = await gameRepository.GetByIdAsync(fallbackGame.Id) ?? fallbackGame;
        return GameMapper.ToGameDto(game);
    }
}
