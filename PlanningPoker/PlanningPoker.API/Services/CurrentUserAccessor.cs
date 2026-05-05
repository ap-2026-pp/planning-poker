using System.Security.Claims;
using PlanningPoker.Domain.Constants;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.API.Services;

/// <summary>
/// Зчитує ідентифікатор поточного користувача з claims у межах HTTP-запиту.
/// </summary>
public class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    /// <summary>
    /// Повертає ідентифікатор поточного авторизованого користувача з claims, якщо він присутній.
    /// </summary>
    /// <returns>Ідентифікатор користувача або <see langword="null"/>.</returns>
    public Guid? GetUserIdOrDefault()
    {
        return TryGetClaimGuid(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Повертає ідентифікатор гостьового учасника, якщо запит авторизований guest access token.
    /// </summary>
    /// <returns>Ідентифікатор guest participant або <see langword="null"/>.</returns>
    public Guid? GetGuestParticipantIdOrDefault()
    {
        var tokenType = httpContextAccessor.HttpContext?.User.FindFirstValue(GuestSessionDefaults.TokenTypeClaimType);
        return !string.Equals(tokenType, GuestSessionDefaults.GuestAccessTokenType, StringComparison.Ordinal) 
            ? null 
            : TryGetClaimGuid(GuestSessionDefaults.ParticipantIdClaimType);
    }

    /// <summary>
    /// Повертає ідентифікатор поточного автентифікованого користувача з HttpContext.
    /// </summary>
    /// <returns>Ідентифікатор користувача.</returns>
    public Guid GetRequiredUserId()
    {
        var userId = GetUserIdOrDefault();
        return userId ?? throw new UnauthorizedAccessException("User id claim is missing or invalid.");
    }

    private Guid? TryGetClaimGuid(string claimType)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new UnauthorizedAccessException("HttpContext is not available.");
        var claimValue = httpContext.User.FindFirstValue(claimType);

        return Guid.TryParse(claimValue, out var id) ? id : null;
    }
}
