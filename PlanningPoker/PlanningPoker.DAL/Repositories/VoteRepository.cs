using Microsoft.EntityFrameworkCore;
using PlanningPoker.DAL.Data;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Repositories;

internal class VoteRepository : BaseRepository<Vote>, IVoteRepository
{
    public VoteRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Vote?> GetVoteAsync(Guid issueId, Guid participantId)
    {
        return await _dbSet.FirstOrDefaultAsync(v => v.IssueId == issueId && v.ParticipantId == participantId);
    }

    public async Task<List<Vote>> GetVotesByIssueIdAsync(Guid issueId)
    {
        return await _dbSet.Include(v => v.Participant).ThenInclude(p => p.User).Where(v => v.IssueId == issueId).ToListAsync();
    }
    public async Task DeleteRangeAsync(IEnumerable<Vote> votes)
    {
        _dbSet.RemoveRange(votes);
    }

    public async Task DeleteAllParticipantVotesAsync(Guid gameId, Guid participantId)
    {
        var votes = await _dbSet.Where(v => v.ParticipantId == participantId && v.Issue.GameId == gameId).ToListAsync();

        if(votes.Any())
        {
            _dbSet.RemoveRange(votes);
        }
    }
}