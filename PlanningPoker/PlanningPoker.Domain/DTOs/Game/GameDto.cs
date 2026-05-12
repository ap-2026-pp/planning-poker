using PlanningPoker.BLL.DTOs.Issue;

namespace PlanningPoker.Domain.DTOs.Game;

public class GameDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? CustomValues { get; set; } 
    public string VotingSystem { get; set; }
    public string RevealPolicy { get; set; }
    public string? InviteCode { get; set; }
    public bool AutoRevealCards { get; set; }
    public bool ShowAverage { get; set; }
    public bool ShowCountdownAnimation { get; set; }
    public bool EnableFunFeature { get; set; } = false;
    public int DefaultTimerMinutes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public ICollection<GameParticipantDto>? Participants { get; set; }
    public ICollection<IssueDto>? Issues { get; set; }
}
