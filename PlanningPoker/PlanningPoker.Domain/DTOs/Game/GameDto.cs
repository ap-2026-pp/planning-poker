using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.Domain.DTOs.Participant;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.DTOs.Game;

public class GameDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; } 
    public string? CustomValues { get; set; } 
    public VotingSystem VotingSystem { get; set; }
    public string? InviteCode { get; set; }
    public RevealPolicy RevealPolicy { get; set; }
    public IssuesPolicy IssuesPolicy { get; set; }
    public bool EnableFunFeatures { get; set; } = false;
    public bool ShowAverage { get; set; }
    public bool ShowCountdownAnimation { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public int DefaultTimerMinutes { get; set; } = 1;
    public bool AutoRevealCards { get; set; }
    public ICollection<GameParticipantDto>? Participants { get; set; }
    public ICollection<IssueDto>? Issues { get; set; }
}
