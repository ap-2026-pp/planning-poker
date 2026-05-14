using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.Domain.DTOs.Participant;
using PlanningPoker.Domain.DTOs.Room;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

public class GameRealtimeService(
    IGameRoomNotifier gameRoomNotifier,
    IUserNotifier userGamesNotifier,
    IParticipantRepository participantRepository) : IGameRealtimeService
{
    public async Task NotifyParticipantJoinedAsync(Game game, GameParticipantDto participant)
    {
        await gameRoomNotifier.NotifyParticipantJoinedAsync(game.Id, participant);
    }

    public async Task NotifyParticipantKickedAsync(Game game, Guid participantId)
    {
        await gameRoomNotifier.NotifyParticipantKickedAsync(game.Id, participantId);
    }

    public async Task NotifyParticipantUpdatedAsync(Game game, GameParticipantDto participant)
    {
        await gameRoomNotifier.NotifyParticipantUpdatedAsync(game.Id, participant);
    }

    public async Task NotifyGameUpdatedAsync(Game game)
    {
        await gameRoomNotifier.NotifyGameUpdatedAsync(game.Id, GameMapper.ToGameDto(game));
        await NotifyAuthorizedUsersAboutGameUpdateAsync(game);
    }

    public async Task NotifyIssueAddedAsync(Game game, IssueDto toDto)
    {
        await gameRoomNotifier.NotifyIssueAddedAsync(game.Id, toDto);
    }

    public async Task NotifyIssueUpdatedAsync(Game game, IssueDto toDto)
    {
        await gameRoomNotifier.NotifyIssueUpdatedAsync(game.Id, toDto);
    }

    public async Task NotifyIssuesImportedAsync(Game game, IEnumerable<IssueDto> select)
    {
        await gameRoomNotifier.NotifyIssuesImportedAsync(game.Id, select);
    }

    public async Task NotifyRoundStateUpdatedAsync(Game game, RoomStateDto toDto)
    {
        await gameRoomNotifier.NotifyRoundStateUpdatedAsync(game.Id, toDto);
    }

    private async Task NotifyAuthorizedUsersAboutGameUpdateAsync(Game game)
    {
        var userIds = await participantRepository.GetAuthorizedUserIdsByGameIdAsync(game.Id);

        foreach (var userId in userIds.Distinct())
        {
            var payload = GameMapper.ToUserGameDto(game, userId);
            await userGamesNotifier.NotifyGameUpdatedAsync(userId, payload);
        }
    }
}