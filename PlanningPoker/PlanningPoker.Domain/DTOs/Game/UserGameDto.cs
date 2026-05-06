using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.DTOs.Game;

public class UserGameDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public ParticipantRole SessionRole { get; set; }
    public bool IsActive { get; set; }
}
