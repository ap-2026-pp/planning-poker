using PlanningPoker.Domain.DTOs.Auth;

namespace PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.BLL.DTOs.Auth;

public interface IUserService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<AuthResponseDto> RefreshTokensAsync(TokenRequestDto dto);
    Task RevokeTokenAsync();
    Task<UserDto> GetCurrentUserAsync();
    Task<UserDto> UpdateCurrentUserDisplayNameAsync(UpdateUserDisplayNameDto dto);
    Task ChangePasswordAsync(ChangePasswordDto dto);
}
