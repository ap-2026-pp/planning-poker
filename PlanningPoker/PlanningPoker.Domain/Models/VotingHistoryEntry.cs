namespace PlanningPoker.Domain.Models;

public class VotingHistoryEntry
{
    public Guid Id { get; set; }
    public int GameId { get; set; }
    public Game Game{ get; set;} = null!;
    public int IssueId { get; set; }
    public Issue Issue{ get; set; } =  null!;
    public string FinalEstimate { get; set; }
    public double? Average { get; set; }
    public double? Agreement { get; set; }
    public DateTime CreatedAt { get; set; }  = DateTime.UtcNow;
}
