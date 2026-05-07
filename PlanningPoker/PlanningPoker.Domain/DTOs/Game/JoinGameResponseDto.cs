namespace PlanningPoker.Domain.DTOs.Game;

public class JoinGameResponseDto
{
    public GameDto Game { get; set; } = null!;
    public Guid CurrentParticipantId { get; set; }
    public string? GuestAccessToken { get; set; }
}
