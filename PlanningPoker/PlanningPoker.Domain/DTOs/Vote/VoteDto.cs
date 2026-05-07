namespace PlanningPoker.Domain.DTOs.VotingHistory;

public class VoteDto
{
    Guid Id;
    Guid IssueId;
    Guid ParticipantId;
    string ParticipantName;
    double Value;
    DateTime CreatedAt; 
    DateTime UpdatedAt;
}
