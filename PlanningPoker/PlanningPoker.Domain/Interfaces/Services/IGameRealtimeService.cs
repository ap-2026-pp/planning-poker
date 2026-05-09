using PlanningPoker.Domain.DTOs.Participant;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IGameRealtimeService
{
    Task NotifyParticipantJoinedAsync(Game game, GameParticipantDto participant);
    Task NotifyParticipantLeftAsync(Game game, Guid participantId);
    Task NotifyParticipantKickedAsync(Game game, Guid participantId);
    Task NotifyParticipantUpdatedAsync(Game game, GameParticipantDto participant);
    Task NotifyGameUpdatedAsync(Game game);
}