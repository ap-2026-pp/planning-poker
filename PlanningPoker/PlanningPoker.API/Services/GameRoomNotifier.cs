using Microsoft.AspNetCore.SignalR;
using PlanningPoker.API.Hubs;
using PlanningPoker.Domain.DTOs.Participant;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.API.Services;

public class GameRoomNotifier(IHubContext<GameRoomHub> hubContext) : IGameRoomNotifier
{
    public Task NotifyParticipantJoinedAsync(Guid gameId, GameParticipantDto participant)
    {
        return NotifyGroupAsync(gameId, GameRoomHubEvents.ParticipantJoined, participant);
    }

    public Task NotifyParticipantLeftAsync(Guid gameId, Guid participantId)
    {
        return NotifyGroupAsync(gameId, GameRoomHubEvents.ParticipantLeft, participantId);
    }

    public Task NotifyParticipantKickedAsync(Guid gameId, Guid participantId)
    {
        return NotifyGroupAsync(gameId, GameRoomHubEvents.ParticipantKicked, participantId);
    }

    public Task NotifyMasterChangedAsync(Guid gameId, GameParticipantDto participant)
    {
        return NotifyGroupAsync(gameId, GameRoomHubEvents.MasterChanged, participant);
    }

    private Task NotifyGroupAsync(Guid gameId, string eventName, object payload)
    {
        return hubContext.Clients
            .Group(GameRoomHub.GetGroupName(gameId))
            .SendAsync(eventName, payload);
    }
}
