using System.ComponentModel.DataAnnotations;

namespace PlanningPoker.Domain.DTOs.Auth;

public record ChangePasswordDto(
    [Required]
    string OldPassword,
    
    [Required]
    string NewPassword
);