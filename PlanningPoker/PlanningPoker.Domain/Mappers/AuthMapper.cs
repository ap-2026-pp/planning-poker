using PlanningPoker.BLL.DTOs.Auth;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Mappers;

public static class AuthMapper
{
    public static UserDto ToUserDto(User user) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName
        );

    public static AuthResponseDto ToAuthResponseDto(
        string accessToken,
        string refreshToken,
        DateTime expiration,
        User user) =>
        new(
            accessToken,
            refreshToken,
            expiration,
            user.Email ?? string.Empty
        );
}
