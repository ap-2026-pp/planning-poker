namespace PlanningPoker.Domain.Models;
using System.ComponentModel.DataAnnotations;

public class GameParticipant
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; }
    public Roles Role { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsConnected { get; set; }
    public Game Game { get; set; }
    public User User { get; set; }
    public ICollection<Vote> Votes { get; set; }
}
