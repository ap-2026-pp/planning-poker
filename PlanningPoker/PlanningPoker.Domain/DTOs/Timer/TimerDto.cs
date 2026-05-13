namespace PlanningPoker.Domain.DTOs.Timer;

public class TimerDto
{
    public Guid GameId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndsAt { get; set; }
    public double RemainingSeconds { get; set; }
    public bool IsExpired { get; set; }
}