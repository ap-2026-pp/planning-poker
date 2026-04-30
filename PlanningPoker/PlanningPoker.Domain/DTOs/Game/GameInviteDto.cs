namespace PlanningPoker.Domain.DTOs.Game;

public class GameInviteDto
{
    public Guid GameId { get; set; }
    public string? InviteCode { get; set; }
    public string? InviteUrl { get; set; }
    public string? QrCodeBase64 { get; set; }
}
