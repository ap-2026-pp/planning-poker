using System.ComponentModel.DataAnnotations;

namespace PlanningPoker.Domain.DTOs.Game;

public class JoinGameRequestDto
{
    [MaxLength(200)]
    public string? DisplayName  { get; set; }
}