namespace PlanningPoker.Domain.Models;
using System.ComponentModel.DataAnnotations;

public class VotingHistoryEntry
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game Game{ get; set;}
    public int IssueId { get; set; }
    public Issue Issue{ get; set; }
    public string FinalEstimate { get; set; }
    public double? Average { get; set; }
    public double? Agreement { get; set; }
    public DateTime CreatedAt { get; set; }
}