namespace PlanningPoker.BLL.DTOs.Auth;

public record UserDto(
    Guid Id,
    string Email,
    string? DisplayName
);
