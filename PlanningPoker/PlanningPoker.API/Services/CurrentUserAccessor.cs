using System.Security.Claims;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.API.Services;

/// <summary>
/// Зчитує ідентифікатор поточного користувача з claims у межах HTTP-запиту.
/// </summary>
public class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    /// <summary>
    /// Повертає ідентифікатор поточного автентифікованого користувача з HttpContext.
    /// </summary>
    /// <returns>Ідентифікатор користувача.</returns>
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
