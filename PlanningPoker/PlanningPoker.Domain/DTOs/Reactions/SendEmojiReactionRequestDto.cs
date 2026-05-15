namespace PlanningPoker.Domain.DTOs.Reactions;

public class SendEmojiReactionRequestDto
{
    public Guid ToParticipantId { get; set; }
    public string? Emoji { get; set; }
}