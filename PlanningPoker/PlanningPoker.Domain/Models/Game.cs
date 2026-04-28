namespace PlanningPoker.Domain.Models;

public class Game
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public VotingSystem VotingSystem { get; set; } = VotingSystem.Custom;
    public string InviteCode { get; set; }
    public bool AutoRevealCards { get; set; }
    public bool ShowAverage { get; set; }
    public bool ShowCountdownAnimation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public ICollection<GameParticipant> Participants { get; set; } = new List<GameParticipant>();
    public ICollection<Issue> Issues { get; set; } = null!;
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}

public enum VotingSystem
{
    Fibonacci,
    TShirtSizes,
    PowersOfTwo,
    Custom
}
