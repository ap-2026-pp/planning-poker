using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

public class ParticipantService(
    IParticipantRepository participantRepository,
    IGameRepository gameRepository,
    ICurrentUserContext currentUserContext) : IParticipantService
{
    public async Task<IEnumerable<GameParticipantDto>> GetGameParticipantsAsync(Guid gameId)
    {
        var participants = await participantRepository.GetGameParticipantsAsync(gameId);
        return (participants ?? []).Select(ParticipantMapper.ToGameParticipantDto);
    }
    
    public async Task<GameDto> JoinGameByInviteCodeAsync(string inviteCode, string? displayName)
    {
        var currentUser = await currentUserContext.GetRequiredUserAsync();
        var game = await gameRepository.GetByInviteCodeAsync(inviteCode)
                   ?? throw new NotFoundException(nameof(Game), nameof(Game.InviteCode), inviteCode);
        var resolvedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? currentUser.DisplayName
            : displayName.Trim();

        var activeParticipant = await participantRepository.GetByUserIdAndGameIdAsync(currentUser.Id, game.Id);
        var existingParticipant = activeParticipant ??
                                  await participantRepository.GetByUserIdAndGameIdIncludingRemovedAsync(currentUser.Id, game.Id);

        if (!game.IsActive)
        {
            if (existingParticipant?.Role != ParticipantRole.Master)
            {
                throw new ForbiddenException("join", "game");
            }

            game.IsActive = true;
            gameRepository.Update(game);
        }

        if (activeParticipant is not null)
        {
            if (!string.Equals(activeParticipant.DisplayName, resolvedDisplayName, StringComparison.Ordinal))
            {
                await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName, game.Id);
                activeParticipant.DisplayName = resolvedDisplayName;
            }

            activeParticipant.IsConnected = true;
            activeParticipant.JoinedAt = DateTime.UtcNow;
            await participantRepository.SaveChangesAsync();

            var updatedGame = await gameRepository.GetByIdAsync(game.Id) ?? game;
            return GameMapper.ToGameDto(updatedGame);
        }

        if (existingParticipant is not null)
        {
            if (!string.Equals(existingParticipant.DisplayName, resolvedDisplayName, StringComparison.Ordinal))
            {
                await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName, game.Id);
                existingParticipant.DisplayName = resolvedDisplayName;
            }

            existingParticipant.IsConnected = true;
            existingParticipant.JoinedAt = DateTime.UtcNow;
            existingParticipant.RemovedAt = null;
            participantRepository.Update(existingParticipant);
        }
        else
        {
            await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName, game.Id);
            await participantRepository.AddAsync(new GameParticipant
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                UserId = currentUser.Id,
                DisplayName = resolvedDisplayName,
                Role = ParticipantRole.Player,
                JoinedAt = DateTime.UtcNow,
                IsConnected = true
            });
        }

        await participantRepository.SaveChangesAsync();
        var refreshedGame = await gameRepository.GetByIdAsync(game.Id) ?? game;
        return GameMapper.ToGameDto(refreshedGame);
    }

    public async Task LeaveGameAsync(Guid gameId)
    {
        var currentUserId = currentUserContext.GetRequiredUserId();
        var game = await GetGameOrThrowAsync(gameId);
        
        var participant = game.Participants.SingleOrDefault(currentParticipant => currentParticipant.UserId == currentUserId);
        if (participant is null)
        {
            throw new NotFoundException("Active game participant was not found.");
        }

        if (participant.Role == ParticipantRole.Master)
        {
            foreach (var currentParticipant in game.Participants)
            {
                participantRepository.RemoveGameParticipant(currentParticipant);
            }

            game.IsActive = false;
            gameRepository.Update(game);
            await participantRepository.SaveChangesAsync();
            return;
        }

        participantRepository.RemoveGameParticipant(participant);
        await participantRepository.SaveChangesAsync();
    }

    public async Task DeleteGameParticipantAsync(Guid gameId, Guid participantId)
    {
        var currentUserId = currentUserContext.GetRequiredUserId();
        await GetGameOrThrowAsync(gameId);

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

    public async Task<GameParticipantDto> UpdateDisplayNameAsync(Guid gameId, string? displayName)
    {
        var currentUser = await currentUserContext.GetRequiredUserAsync();
        await GetGameOrThrowAsync(gameId);
        
        var currentUserParticipant = await participantRepository.GetByUserIdAndGameIdAsync(currentUser.Id, gameId)
                                     ?? throw new NotFoundException("User participant was not found in the game.");
        
        var resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? currentUser.DisplayName : displayName.Trim();

        if (!string.Equals(currentUserParticipant.DisplayName, resolvedDisplayName, StringComparison.Ordinal))
        {
            await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName, gameId);
            currentUserParticipant.DisplayName = resolvedDisplayName;
        }

        await participantRepository.SaveChangesAsync();
        return ParticipantMapper.ToGameParticipantDto(currentUserParticipant);
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
}
