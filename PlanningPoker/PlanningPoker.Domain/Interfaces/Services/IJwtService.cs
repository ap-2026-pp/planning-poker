namespace PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.BLL.DTOs.Auth;
using System.Security.Claims;

public interface IJwtService
{
    string GenerateAccessToken(Guid userId, string email);
    string GenerateRefreshToken();
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
}
