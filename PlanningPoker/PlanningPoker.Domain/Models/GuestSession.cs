namespace PlanningPoker.Domain.Models;

public class GuestSession
{
    public Guid Id { get; set; }
    public Guid ParticipantId { get; set; }
    public GameParticipant Participant { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
