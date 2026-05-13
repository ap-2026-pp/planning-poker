using PlanningPoker.Domain.DTOs.Vote;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Mappers;

public static class VoteMapper
{
    public static VoteDto ToDto(Vote vote)
    {
        if (vote == null) return null!;
        return new VoteDto
        {
            Id = vote.Id,
            IssueId = vote.IssueId,
            ParticipantId = vote.ParticipantId,
            DisplayName = vote.Participant?.User?.DisplayName,
            Estimate = vote.Estimate,
            CreatedAt = vote.CreatedAt
        };
    }
}



