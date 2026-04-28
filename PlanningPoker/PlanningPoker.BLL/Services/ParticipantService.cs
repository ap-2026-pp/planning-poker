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
    public async Task<IEnumerable<GameParticipantDto>?> GetGameParticipantsAsync(Guid gameId)
    {
        var participants = await participantRepository.GetGameParticipantsAsync(gameId);
        return (participants ?? []).Select(ParticipantMapper.ToGameParticipantDto);
    }
    
    public async Task<GameDto> JoinGameByInviteCodeAsync(string inviteCode, string displayName)
    {
        var currentUserId = currentUserService.GetRequiredUserId();
        
        var game = await gameRepository.GetByInviteCodeAsync(inviteCode);
        if (game is null)
        {
            throw new NotFoundException(nameof(Game), nameof(Game.InviteCode), inviteCode);
        }

        if (!game.IsActive)
        {
            throw new ForbiddenException("join", "game");
        }
        
        var exists = await participantRepository.ExistsByDisplayNameAsync(displayName, game.Id);
        if (exists)
        {
            throw new GameAlreadyExistsException(displayName); // TODO change to ResourceNotFoundException
        }

        var activeParticipant = await participantRepository.GetByUserIdAndGameIdAsync(currentUserId, game.Id);
        if (activeParticipant is not null)
        {
            activeParticipant.IsConnected = true;
            activeParticipant.JoinedAt = DateTime.UtcNow;
            await participantRepository.SaveChangesAsync();
            var g = await gameRepository.GetByIdAsync(game.Id) ?? game;
            return GameMapper.ToGameDto(g);
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
        var joinedGame = await gameRepository.GetByIdAsync(game.Id) ?? game;
        return GameMapper.ToGameDto(joinedGame);
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
        var game = await gameRepository.GetByIdAsync(gameId);
        if (game is null)
        {
            throw new NotFoundException(nameof(Game), gameId);
        }
        
        var currentUserParticipant = await participantRepository.GetByUserIdAndGameIdAsync(currentUserId, gameId);
        if (currentUserParticipant is null || currentUserParticipant.Role != ParticipantRole.Master)
        {
            throw new ForbiddenException("delete", "participant");
        }
        
        var participant = await participantRepository.GetActiveByIdAsync(participantId);
        if (participant is null || participant.GameId != gameId)
        {
            throw new NotFoundException(nameof(GameParticipant), participantId);
        }
        
        participantRepository.RemoveGameParticipant(participant);
        await participantRepository.SaveChangesAsync();
    }

    public async Task<GameParticipant> UpdateDisplayNameAsync(Guid gameId, string displayName)
    {
        var currentUserId = currentUserService.GetRequiredUserId();
        var game = await gameRepository.GetByIdAsync(gameId);
        if (game is null)
        {
            throw new NotFoundException(nameof(Game), gameId);
        }
        
        var exists = await participantRepository.ExistsByDisplayNameAsync(displayName, game.Id);
        if (exists)
        {
            throw new GameAlreadyExistsException(displayName); // TODO change exception
        }
        
        var currentUserParticipant = await participantRepository.GetByUserIdAndGameIdAsync(currentUserId,  gameId);
        if (currentUserParticipant is null)
        {
            throw new NotFoundException("User participant was not found in the game.");
        }
        
        currentUserParticipant.DisplayName = displayName;
        await participantRepository.SaveChangesAsync();
        return currentUserParticipant;
    }
}
