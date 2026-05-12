namespace PlanningPoker.Domain.Interfaces.Repositories;

using PlanningPoker.Domain.Models;

public interface IVoteRepository : IBaseRepository<Vote>
{
    public Task<Vote?> GetVoteAsync(Guid issueid, Guid participantId);
    public Task<List<Vote>> GetVotesByIssueIdAsync(Guid issueId);
    public Task DeleteRangeAsync(IEnumerable<Vote> votes);
    public Task DeleteAllParticipantVotesAsync(Guid gameId, Guid participantId);
}


