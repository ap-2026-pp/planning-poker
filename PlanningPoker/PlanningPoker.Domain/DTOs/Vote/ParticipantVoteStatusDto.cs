namespace PlanningPoker.Domain.DTOs.Vote;

public class ParticipantVoteStatusDto
{
    public Guid ParticipantId { get; set; }
    public string DisplayName { get; set; } = null!;
    public bool HasVoted { get; set; }
    public string? VoteValue { get; set; } // null якщо !IsRevealed
}