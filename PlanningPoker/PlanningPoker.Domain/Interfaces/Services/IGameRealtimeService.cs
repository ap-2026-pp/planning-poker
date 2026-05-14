using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.Domain.DTOs.Participant;
using PlanningPoker.Domain.DTOs.Room;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IGameRealtimeService
{
    Task NotifyParticipantJoinedAsync(Game game, GameParticipantDto participant);
    Task NotifyParticipantKickedAsync(Game game, Guid participantId);
    Task NotifyParticipantUpdatedAsync(Game game, GameParticipantDto participant);
    Task NotifyGameUpdatedAsync(Game game);
    Task NotifyIssueAddedAsync(Game gameId, IssueDto toDto);
    Task NotifyIssueUpdatedAsync(Game game, IssueDto toDto);
    Task NotifyIssuesImportedAsync(Game game, IEnumerable<IssueDto> select);
    Task NotifyRoundStateUpdatedAsync(Game game, RoomStateDto toDto);
}