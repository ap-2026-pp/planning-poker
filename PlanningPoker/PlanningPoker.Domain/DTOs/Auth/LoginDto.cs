namespace PlanningPoker.BLL.DTOs.Auth;
using System.ComponentModel.DataAnnotations;
public record LoginDto(
    [Required]
    [EmailAddress]
    string Email,

    [Required]
    string Password
);