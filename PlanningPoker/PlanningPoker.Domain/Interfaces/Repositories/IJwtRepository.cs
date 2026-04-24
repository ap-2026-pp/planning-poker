namespace PlanningPoker.Domain.Interfaces.Repositories;
using System.Security.Claims;

public interface IJwtRepository
{
   string GenerateAccessToken(Guid userId, string email);
   string GenerateRefreshToken();
   ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
}