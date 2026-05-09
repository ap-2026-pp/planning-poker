using PlanningPoker.Domain.DTOs.Participant;
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
        await NotifyAuthorizedUsersAboutGameUpdateAsync(game);
    }

    public async Task NotifyParticipantLeftAsync(Game game, Guid participantId)
    {
        await gameRoomNotifier.NotifyParticipantLeftAsync(game.Id, participantId);
        await NotifyAuthorizedUsersAboutGameUpdateAsync(game);
    }

    public async Task NotifyParticipantKickedAsync(Game game, Guid participantId)
    {
        await gameRoomNotifier.NotifyParticipantKickedAsync(game.Id, participantId);
        await NotifyAuthorizedUsersAboutGameUpdateAsync(game);
    }

    public async Task NotifyParticipantUpdatedAsync(Game game, GameParticipantDto participant)
    {
        await gameRoomNotifier.NotifyParticipantUpdatedAsync(game.Id, participant);
        await NotifyAuthorizedUsersAboutGameUpdateAsync(game);
    }

    public async Task NotifyGameUpdatedAsync(Game game)
    {
        await gameRoomNotifier.NotifyGameUpdatedAsync(game.Id, GameMapper.ToGameDto(game));
        await NotifyAuthorizedUsersAboutGameUpdateAsync(game);
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