using System.ComponentModel.DataAnnotations;

namespace PlanningPoker.Domain.DTOs.Auth;

public record UpdateUserDisplayNameDto(
    [Required]
    [MaxLength(200)]
    string DisplayName
);
