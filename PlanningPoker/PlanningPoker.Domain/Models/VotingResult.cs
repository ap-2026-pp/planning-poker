namespace PlanningPoker.Domain.Models;
public class VotingResult
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;
    public Guid IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public string FinalEstimate { get; set; } = null!;
    public double? Average { get; set; }
    public double? Agreement { get; set; }
    public DateTime CreatedAt { get; set; }
}