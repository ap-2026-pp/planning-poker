namespace PlanningPoker.BLL.DTOs.Auth;
using System.ComponentModel.DataAnnotations;

public record AuthResponseDto(
   [Required]
   string AccessToken,

   [Required]
   string RefreshToken,

   [Required]
   DateTime Expiration,

   [Required]
   string Email
);