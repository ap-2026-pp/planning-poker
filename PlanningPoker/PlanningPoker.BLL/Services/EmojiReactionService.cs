using PlanningPoker.BLL.Constants;
using PlanningPoker.Domain.DTOs.Reactions;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

public class EmojiReactionService(
    IGameRepository gameRepository,
    IParticipantRepository participantRepository,
    IGameRealtimeService gameRealtimeService) : IEmojiReactionService
{
    public async Task SendEmojiReactionAsync(
        Guid gameId,
        Guid currentParticipantId,
        SendEmojiReactionRequestDto request)
    {
        var game = await gameRepository.GetByIdAsync(gameId) 
            ?? throw new NotFoundException(nameof(Game), gameId);
        
        if (request.ToParticipantId == Guid.Empty)
        {
            throw new InvalidOperationException("Target participant is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Emoji))
        {
            throw new InvalidOperationException("Emoji is required.");
        }

        var emoji = request.Emoji.Trim();

        if (emoji.Length > 16)
        {
            throw new InvalidOperationException("Emoji value is too long.");
        }

        var currentParticipant = await participantRepository.GetActiveByIdAsync(currentParticipantId);

        if (currentParticipant is null ||
            currentParticipant.GameId != gameId ||
            currentParticipant.RemovedAt is not null)
        {
            throw new ForbiddenException(
                AccessControlConstants.UpdateAction,
                AccessControlConstants.GameResource);
        }

        var targetParticipant = await participantRepository.GetActiveByIdAsync(request.ToParticipantId);

        if (targetParticipant is null ||
            targetParticipant.GameId != gameId ||
            targetParticipant.RemovedAt is not null)
        {
            throw new NotFoundException("Target participant", request.ToParticipantId);
        }

        var reaction = new EmojiReactionDto
        {
            GameId = gameId,
            FromParticipantId = currentParticipant.Id,
            FromDisplayName = currentParticipant.DisplayName,
            ToParticipantId = targetParticipant.Id,
            Emoji = emoji,
            CreatedAt = DateTime.UtcNow
        };

        await gameRealtimeService.NotifyEmojiReactionAsync(game, reaction);
    }
}
