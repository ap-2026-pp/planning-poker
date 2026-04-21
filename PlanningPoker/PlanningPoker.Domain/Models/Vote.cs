namespace PlanningPoker.Domain.Models;
using System.ComponentModel.DataAnnotations;
public class Vote
{
    public int Id { get; set; }

    public int GameId { get; set; }
    public Game Game { get; set;}

    public int IssueId { get; set; }
    public Issue Issue{ get; set;}

    public int ParticipantId { get; set; }
    public Participant Participant { get; set;}

    [Required]
    [MaxLength(50)]
    public string Value { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
