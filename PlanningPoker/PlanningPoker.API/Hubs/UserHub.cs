using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PlanningPoker.API.Hubs;

[Authorize]
public class UserHub : Hub
{
    public const string HubRoute = "/hubs/user-games";

    public static string GetGroupName(Guid userId) => $"user-games:{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserIdOrDefault();

        if (userId is null)
        {
            Context.Abort(); 
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(userId.Value));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserIdOrDefault();

        if (userId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(userId.Value));
        }

        await base.OnDisconnectedAsync(exception);
    }

    private Guid? GetUserIdOrDefault()
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var userId) ? userId : null;
    }
}