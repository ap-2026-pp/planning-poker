namespace PlanningPoker.Domain.DTOs.Vote;

/// <summary>
/// Голос одного учасника в конкретному раунді
/// </summary>
public class VoteResultDto
{
    public Guid ParticipantId { get; set;}
    public string DisplayName { get; set;} = string.Empty;
    public string VoteValue{ get; set; } = string.Empty;
}
