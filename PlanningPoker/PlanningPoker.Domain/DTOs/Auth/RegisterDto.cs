namespace PlanningPoker.BLL.DTOs.Auth;
using System.ComponentModel.DataAnnotations;

public record RegisterDto(
    [Required]
    [EmailAddress]
    string Email,

    [Required]
    string Password
);