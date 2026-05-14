using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.DTOs.Participant;

public class GameParticipantDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? DisplayName { get; set; }
    public ParticipantRole Role { get; set; } = ParticipantRole.Player;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsConnected { get; set; }
    public bool CanRevealCards { get; set; }
    public bool CanManageIssues { get; set; }
}
