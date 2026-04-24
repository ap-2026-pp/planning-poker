using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.DTOs.Game;

public class GameParticipantDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; }
    public Role Role { get; set; }  = Role.Player;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsConnected { get; set; }
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public ICollection<Issue> Issues { get; set; } = new List<Issue>();
}