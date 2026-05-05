namespace PlanningPoker.Domain.Interfaces.Services;
using System.Security.Claims;

public interface IJwtService
{
    string GenerateAccessToken(Guid userId, string email, IList<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
}
