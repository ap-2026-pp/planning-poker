namespace PlanningPoker.Domain.Models;


public class Issue
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;
    public string Url { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public int Order { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsRemoved{ get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public GameParticipant CreatedByParticipant { get; set; } = null!;
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public ICollection<VotingResult> VotingResults { get; set; } = new List<VotingResult>();
}
