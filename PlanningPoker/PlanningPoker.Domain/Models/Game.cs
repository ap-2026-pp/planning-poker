namespace PlanningPoker.Domain.Models;
public class Game
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string VotingSystem { get; set; }
    public string InviteCode { get; set; }
    public bool AutoRevealCards { get; set; }
    public bool ShowAverage { get; set; }
    public bool ShowCountdownAnimation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public ICollection<GameParticipant> Participants { get; set; }
    public ICollection<Issue> Issues { get; set; } = null!;
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public ICollection<VotingHistory> VotingHistories { get; set; }  = new List<VotingHistory>();
}

