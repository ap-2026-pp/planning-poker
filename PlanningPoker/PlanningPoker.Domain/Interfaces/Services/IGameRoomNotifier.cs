using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.DTOs.Participant;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IGameRoomNotifier
{
    Task NotifyParticipantJoinedAsync(Guid gameId, GameParticipantDto participant);

    Task NotifyParticipantKickedAsync(Guid gameId, Guid participantId);

    Task NotifyParticipantUpdatedAsync(Guid gameId, GameParticipantDto participant);

    Task NotifyGameUpdatedAsync(Guid gameId, GameDto game);
    Task NotifyIssueAddedAsync(Guid gameId, IssueDto issue);
}
