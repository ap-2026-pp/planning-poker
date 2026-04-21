namespace PlanningPoker.Domain.Models;
using System.ComponentModel.DataAnnotations;
public class Vote
{
    public Guid Id { get; set;}
    public Guid IssueId { get; set; } 
    public Issuer Issuer { get; set;} = null!;
    public Guid ParticipantId { get; set; }
    public GameParticipant Participant { get; set;} = null!;
    public string Value { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } 
}



