using System.Security.Claims;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.API.Services;

public class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public Guid GetRequiredUserId()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new UnauthorizedAccessException("HttpContext is not available.");
        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User id claim is missing or invalid.");
        }

        return userId;
    }
}
