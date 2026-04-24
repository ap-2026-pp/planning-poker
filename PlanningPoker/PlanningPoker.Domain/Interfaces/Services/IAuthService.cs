namespace PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.BLL.DTOs.Auth;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<AuthResponseDto> RefreshTokensAsync(TokenRequestDto dto);
    Task RevokeTokenAsync(Guid userId);
    Task<UserDto> GetCurrentUserAsync(Guid userId);
}
