namespace PlanningPoker.Domain.Models;

public class Vote
{
    public Guid Id { get; set;}
    public Guid IssueId { get; set; } 
    public Issue Issue { get; set;} = null!;
    public Guid ParticipantId { get; set; }
    public GameParticipant Participant { get; set;} = null!;
    public string Estimate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } 
}



