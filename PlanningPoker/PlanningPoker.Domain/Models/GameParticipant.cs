namespace PlanningPoker.Domain.Models;


public class GameParticipant
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string DisplayName { get; set; }
    public Role Role { get; set; }  = Role.Player;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsConnected { get; set; }
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
