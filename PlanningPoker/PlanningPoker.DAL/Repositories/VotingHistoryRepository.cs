using Microsoft.EntityFrameworkCore;
using PlanningPoker.DAL.Data;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Repositories;

internal class VotingHistoryRepository : BaseRepository<VotingResult>, IVotingHistoryRepository
{
    public VotingHistoryRepository(AppDbContext context) : base(context)
    {
       
    }

    public async Task<List<VotingResult>> GetHistoryRawAsync(Guid gameId)
    {
        return await _context.VotingResults
            .Where(result =>
                result.GameId == gameId &&
                !result.Issue.IsRemoved)
            .Include(result => result.Issue)
                .ThenInclude(issue => issue.Votes)
                    .ThenInclude(vote => vote.Participant)
            .ToListAsync();
    }

    public async Task<VotingResult?> GetHistoryDetailsRawAsync(
        Guid gameId,
        Guid entryId)
    {
        return await _context.VotingResults
            .Where(result =>
                result.GameId == gameId &&
                result.Id == entryId &&
                !result.Issue.IsRemoved)
            .Include(result => result.Issue)
                .ThenInclude(issue => issue.Votes)
                    .ThenInclude(vote => vote.Participant)
            .FirstOrDefaultAsync();
    }

    public async Task<VotingResult?> GetByIssueIdAsync(Guid issueId)
    {
        return await _dbSet.FirstOrDefaultAsync(r => r.IssueId == issueId);
    }
}
