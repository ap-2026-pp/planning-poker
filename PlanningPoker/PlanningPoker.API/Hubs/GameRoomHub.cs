using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PlanningPoker.Domain.Constants;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.API.Services;

namespace PlanningPoker.API.Hubs;

[Authorize]
public class GameRoomHub(
    IParticipantRepository participantRepository,
    IGameRoomNotifier gameRoomNotifier,
    GameRoomConnectionTracker connectionTracker) : Hub
{
    public const string HubRoute = "/hubs/game-room";

    public static string GetGroupName(Guid gameId) => $"game:{gameId}";

    public override async Task OnConnectedAsync()
    {
        if (!TryGetGameId(out var gameId))
        {
            Context.Abort();
            return;
        }

        var participant = await participantRepository.GetCurrentParticipantAsync(
            gameId,
            GetUserIdOrDefault(),
            GetGuestParticipantIdOrDefault());

        if (participant is null)
        {
            Context.Abort();
            return;
        }

        var isFirstConnection = connectionTracker.RegisterConnection(participant.Id, Context.ConnectionId);

        if (isFirstConnection && !participant.IsConnected)
        {
            participant.IsConnected = true;
            await participantRepository.SaveChangesAsync();
            await gameRoomNotifier.NotifyParticipantUpdatedAsync(gameId, ParticipantMapper.ToGameParticipantDto(participant));
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(gameId));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionStateChange = connectionTracker.UnregisterConnection(Context.ConnectionId);

        if (TryGetGameId(out var gameId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(gameId));
        }

        if (connectionStateChange is { IsLastConnection: true } stateChange)
        {
            var participant = await participantRepository.GetActiveByIdAsync(stateChange.ParticipantId);

            if (participant is not null && participant.IsConnected)
            {
                participantRepository.MarkParticipantOffline(participant);
                await participantRepository.SaveChangesAsync();
                await gameRoomNotifier.NotifyParticipantUpdatedAsync(
                    participant.GameId,
                    ParticipantMapper.ToGameParticipantDto(participant));
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private bool TryGetGameId(out Guid gameId)
    {
        var gameIdRaw = Context.GetHttpContext()?.Request.Query["gameId"].ToString();
        return Guid.TryParse(gameIdRaw, out gameId);
    }

    private Guid? GetUserIdOrDefault()
    {
        return TryGetClaimGuid(ClaimTypes.NameIdentifier);
    }

    private Guid? GetGuestParticipantIdOrDefault()
    {
        var tokenType = Context.User?.FindFirst(GuestSessionDefaults.TokenTypeClaimType)?.Value;
        return !string.Equals(tokenType, GuestSessionDefaults.GuestAccessTokenType, StringComparison.Ordinal)
            ? null
            : TryGetClaimGuid(GuestSessionDefaults.ParticipantIdClaimType);
    }

    private Guid? TryGetClaimGuid(string claimType)
    {
        var claimValue = Context.User?.FindFirst(claimType)?.Value;
        return Guid.TryParse(claimValue, out var id) ? id : null;
    }
}
