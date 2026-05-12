namespace PlanningPoker.Domain.DTOs.Vote;

public class VoteDto
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid ParticipantId { get; set; }
    public string? DisplayName { get; set; }
    public string Estimate { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
