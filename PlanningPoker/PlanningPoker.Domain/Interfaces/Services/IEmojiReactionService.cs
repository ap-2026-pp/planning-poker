using PlanningPoker.Domain.DTOs.Reactions;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IEmojiReactionService
{
    Task SendEmojiReactionAsync(Guid gameId, Guid currentParticipantId, SendEmojiReactionRequestDto request);
}
