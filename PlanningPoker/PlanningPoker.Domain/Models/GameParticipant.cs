namespace PlanningPoker.Domain.Models;

public class GameParticipant
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public string? DisplayName { get; set; }
    public ParticipantRole Role { get; set; }  = ParticipantRole.Player;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsConnected { get; set; }
    public DateTime? RemovedAt { get; set; }
    public bool CanManageIssues { get; set; }
    public bool CanRevealCards { get; set; }
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public ICollection<Issue> Issues { get; set; } = new List<Issue>();
}
