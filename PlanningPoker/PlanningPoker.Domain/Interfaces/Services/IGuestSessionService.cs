using System.Security.Claims;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IGuestSessionService
{
    string GenerateGuestAccessToken(Guid participantId);
    Task<GuestSession> CreateGuestSession(Guid participantId, string accessToken);
    Task<bool> IsGuestAccessTokenActiveAsync(string accessToken);
    Task RevokeGuestSessionsAsync(Guid participantId);
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
}
