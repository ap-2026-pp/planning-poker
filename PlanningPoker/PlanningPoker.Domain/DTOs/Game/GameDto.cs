using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.DTOs.Game;

public class GameDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public VotingSystem VotingSystem { get; set; }
    public string? InviteCode { get; set; }
    public RevealPolicy RevealPolicy { get; set; }
    public IssuesPolicy IssuesPolicy { get; set; }
    public bool AutoRevealCards { get; set; }
    public bool ShowAverage { get; set; }
    public bool ShowCountdownAnimation { get; set; }
    public bool EnableFunFeatures { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public ICollection<GameParticipantDto>? Participants { get; set; }
    public ICollection<IssueDto>? Issues { get; set; }
}
