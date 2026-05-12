namespace PlanningPoker.Domain.DTOs.Game;

public class ParticipantPermissionsDto
{
    public Guid ParticipantId { get; set; }
    public bool CanRevealCards { get; set; }
    public bool CanManageIssues { get; set; }

}