namespace PlanningPoker.BLL.DTOs.Auth;

public record TokenRequestDto(
    string AccessToken,
    string RefreshToken
);