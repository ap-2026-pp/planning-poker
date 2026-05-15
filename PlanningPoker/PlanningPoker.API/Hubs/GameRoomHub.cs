using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.API.Services;
using PlanningPoker.Domain.Constants;
using PlanningPoker.Domain.DTOs.Reactions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;

namespace PlanningPoker.API.Hubs;

[Authorize]
public class GameRoomHub(
    IParticipantRepository participantRepository,
    IGameRoomNotifier gameRoomNotifier,
    GameRoomConnectionTracker connectionTracker,
    IEmojiReactionService emojiReactionService,
    IServiceScopeFactory serviceScopeFactory) : Hub
{
    public const string HubRoute = "/hubs/game-room";
    private const string GameIdContextItemKey = "gameId";
    private const string ParticipantIdContextItemKey = "participantId";

    public static string GetGroupName(Guid gameId) => $"game:{gameId}";

    public override async Task OnConnectedAsync()
    {
        if (!TryGetGameId(out var gameId))
        {
            Context.Abort();
            return;
        }

        Context.Items[GameIdContextItemKey] = gameId;

        var participant = await participantRepository.GetCurrentParticipantAsync(
            gameId,
            GetUserIdOrDefault(),
            GetGuestParticipantIdOrDefault());

        if (participant is null)
        {
            Context.Abort();
            return;
        }

        Context.Items[ParticipantIdContextItemKey] = participant.Id;

        var isFirstConnection = connectionTracker.RegisterConnection(
            participant.Id,
            Context.ConnectionId);

        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(gameId));

        if (isFirstConnection && !participant.IsConnected)
        {
            participant.IsConnected = true;
            await participantRepository.SaveChangesAsync();

            await gameRoomNotifier.NotifyParticipantUpdatedAsync(
                gameId,
                ParticipantMapper.ToGameParticipantDto(participant));
        }
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
            connectionTracker.ScheduleOfflineTransition(
                stateChange.ParticipantId,
                MarkParticipantOfflineAsync);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendEmojiReaction(SendEmojiReactionRequestDto request)
    {
        var gameId = GetGameIdFromContext();
        var currentParticipantId = await GetCurrentParticipantIdFromContextAsync(gameId);

        try
        {
            await emojiReactionService.SendEmojiReactionAsync(gameId, currentParticipantId, request);
        }
        catch (Exception exception)
        {
            throw new HubException(exception.Message);
        }
    }

    private Guid GetGameIdFromContext()
    {
        return !TryGetGameId(out var gameId) 
            ? throw new HubException("Game id is required.") 
            : gameId;
    }

    private async Task MarkParticipantOfflineAsync(Guid participantId)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();

        var scopedParticipantRepository = scope.ServiceProvider.GetRequiredService<IParticipantRepository>();
        var scopedGameRoomNotifier = scope.ServiceProvider.GetRequiredService<IGameRoomNotifier>();
        var participant = await scopedParticipantRepository.GetActiveByIdAsync(participantId);

        if (participant is null || !participant.IsConnected)
        {
            return;
        }

        scopedParticipantRepository.MarkParticipantOffline(participant);
        await scopedParticipantRepository.SaveChangesAsync();

        await scopedGameRoomNotifier.NotifyParticipantUpdatedAsync(
            participant.GameId,
            ParticipantMapper.ToGameParticipantDto(participant));
    }

    private async Task<Guid> GetCurrentParticipantIdFromContextAsync(Guid gameId)
    {
        if (Context.Items.TryGetValue(ParticipantIdContextItemKey, out var cachedParticipantId) &&
            cachedParticipantId is Guid storedParticipantId)
        {
            return storedParticipantId;
        }

        var participant = await participantRepository.GetCurrentParticipantAsync(
            gameId,
            GetUserIdOrDefault(),
            GetGuestParticipantIdOrDefault());

        if (participant is null)
        {
            throw new HubException("Current participant was not found.");
        }

        Context.Items[ParticipantIdContextItemKey] = participant.Id;
        return participant.Id;
    }

    private bool TryGetGameId(out Guid gameId)
    {
        if (Context.Items.TryGetValue(GameIdContextItemKey, out var cachedGameId) &&
            cachedGameId is Guid storedGameId)
        {
            gameId = storedGameId;
            return true;
        }

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

        return !string.Equals(
                tokenType,
                GuestSessionDefaults.GuestAccessTokenType,
                StringComparison.Ordinal)
            ? null
            : TryGetClaimGuid(GuestSessionDefaults.ParticipantIdClaimType);
    }

    private Guid? TryGetClaimGuid(string claimType)
    {
        var claimValue = Context.User?.FindFirst(claimType)?.Value;
        return Guid.TryParse(claimValue, out var id) ? id : null;
    }
}
