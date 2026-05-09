using Microsoft.AspNetCore.SignalR;
using PlanningPoker.API.Hubs;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.API.Services;

public class UserNotifier(IHubContext<UserHub> hubContext) : IUserNotifier
{
    public Task NotifyGameUpdatedAsync(Guid userId, UserGameDto payload)
    {
        return hubContext.Clients
            .Group(UserHub.GetGroupName(userId))
            .SendAsync(UserHubEvents.GameUpdated, payload);
    }
}