using Microsoft.AspNetCore.SignalR;
using PlanningPoker.API.Hubs;
using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.DTOs.Participant;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.API.Services;

public class GameRoomNotifier(IHubContext<GameRoomHub> hubContext) : IGameRoomNotifier
{
    public Task NotifyParticipantJoinedAsync(Guid gameId, GameParticipantDto participant)
        => NotifyGroupAsync(gameId, GameRoomHubEvents.ParticipantJoined, participant);

    public Task NotifyParticipantKickedAsync(Guid gameId, Guid participantId)
        => NotifyGroupAsync(gameId, GameRoomHubEvents.ParticipantKicked, participantId);

    public Task NotifyParticipantUpdatedAsync(Guid gameId, GameParticipantDto participant)
        => NotifyGroupAsync(gameId, GameRoomHubEvents.ParticipantUpdated, participant);

    public Task NotifyGameUpdatedAsync(Guid gameId, GameDto game)
        => NotifyGroupAsync(gameId, GameRoomHubEvents.GameUpdated, game);

    public Task NotifyIssueAddedAsync(Guid gameId, IssueDto issue) 
        => NotifyGroupAsync(gameId, GameRoomHubEvents.IssueCreated, issue);
    
    public Task NotifyIssueUpdatedAsync(Guid gameId, IssueDto issue)
        => NotifyGroupAsync(gameId, GameRoomHubEvents.IssueUpdated, issue);

    public Task NotifyIssuesImportedAsync(Guid gameId, IEnumerable<IssueDto> issues) 
        => NotifyGroupAsync(gameId, GameRoomHubEvents.IssuesImported, issues);

    private Task NotifyGroupAsync(Guid gameId, string eventName, object payload)
    {
        return hubContext.Clients
            .Group(GameRoomHub.GetGroupName(gameId))
            .SendAsync(eventName, payload);
    }
}
