namespace PlanningPoker.Domain.Models;

public class GameTimer
{
    public Guid GameId { get; set; }
    
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime EndsAt { get; set; }
    public bool AutoReset { get; set; }
    public Game Game { get; set; } = null!;
}

