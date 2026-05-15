namespace PlanningPoker.Domain.DTOs.Reactions;

public class EmojiReactionDto
{
    public Guid GameId { get; set; }
    public Guid FromParticipantId { get; set; }
    public string? FromDisplayName { get; set; } 
    public Guid ToParticipantId { get; set; }
    public string? Emoji { get; set; }
    public DateTime CreatedAt { get; set; }
}