namespace PlanningPoker.Domain.DTOs.Game;

public class GameInviteDto
{
    public Guid GameId { get; set; }
    public string InviteCode { get; set; } = string.Empty;
    public string InviteUrl { get; set; } = string.Empty;
    public string QrCodeBase64 { get; set; } = string.Empty;
}
