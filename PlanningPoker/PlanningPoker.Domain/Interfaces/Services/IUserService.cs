namespace PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.BLL.DTOs.Auth;

public interface IUserService
{
    Task<UserDto> RegisterAsync(RegisterDto dto);
    Task<UserDto> LoginAsync(LoginDto dto);
    Task RefreshTokenAsync();
    Task RevokeTokenAsync();
    Task<UserDto> GetCurrentUserAsync();
}
