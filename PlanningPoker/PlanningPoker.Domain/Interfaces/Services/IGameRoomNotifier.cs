using PlanningPoker.Domain.DTOs.Participant;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IGameRoomNotifier
{
    Task NotifyParticipantJoinedAsync(Guid gameId, GameParticipantDto participant);
    Task NotifyParticipantLeftAsync(Guid gameId, Guid participantId);
    Task NotifyParticipantKickedAsync(Guid gameId, Guid participantId);
    Task NotifyMasterChangedAsync(Guid gameId, GameParticipantDto participant);
}
