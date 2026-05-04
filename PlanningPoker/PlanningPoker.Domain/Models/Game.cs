namespace PlanningPoker.Domain.Models;

public class Game
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public VotingSystem VotingSystem { get; set; } = VotingSystem.Custom;
    public string InviteCode { get; set; }
    public RevealPolicy RevealPolicy { get; set; } = RevealPolicy.MasterOnly;
    public IssuesPolicy IssuesPolicy { get; set; } = IssuesPolicy.MasterOnly;
    public bool AutoRevealCards { get; set; } = false;
    public bool ShowAverage { get; set; } = true;
    public bool ShowCountdownAnimation { get; set; } = true;
    public bool EnableFunFeatures { get; set; } = false;
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


/// <summary>
/// Політика доступу до розкриття результатів голосування.
/// </summary>
public enum RevealPolicy
{
    MasterOnly,
    Everyone
}

/// <summary>
/// Політика доступу до управління задачами (issues) у грі.
/// </summary>
public enum IssuesPolicy
{
    MasterOnly,
    Everyone
}