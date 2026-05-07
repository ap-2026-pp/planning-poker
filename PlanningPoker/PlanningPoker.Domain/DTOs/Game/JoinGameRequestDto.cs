using System.ComponentModel.DataAnnotations;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.DTOs.Game;

public class JoinGameRequestDto
{
    [MaxLength(200)]
    public string? DisplayName  { get; set; }
    public ParticipantRole? ParticipantRole { get; set; }
}