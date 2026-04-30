using Microsoft.EntityFrameworkCore;
using PlanningPoker.DAL.Data;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Repositories;

internal class IssueRepository : BaseRepository<Issue>, IIssueRepository
{
    public IssueRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Issue>> GetByGameIdAsync(Guid gameId)
    {
        return await _dbSet
            .Where(i => i.GameId == gameId && !i.IsRemoved)
            .OrderBy(i => i.Order)
            .ToListAsync();
    }

    public async Task<Issue?> GetByGameAndIssueAsync(Guid gameId, Guid issueId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(i =>
                i.GameId == gameId &&
                i.Id == issueId &&
                !i.IsRemoved);
    }

    public async Task<int> GetNextOrderAsync(Guid gameId)
    {
        var maxOrder = await _dbSet
            .Where(i => i.GameId == gameId && !i.IsRemoved)
            .Select(i => (int?)i.Order)
            .MaxAsync();

        return (maxOrder ?? 0) + 1;
    }

    public async Task<IEnumerable<Issue>> GetIssuesByIdsAsync(Guid gameId, List<Guid> issueIds)
    {
        return await _dbSet
            .Where(i =>
                i.GameId == gameId &&
                issueIds.Contains(i.Id) &&
                !i.IsRemoved)
            .ToListAsync();
    }

    public async Task ClearCurrentIssueAsync(Guid gameId)
    {
        var issues = await _dbSet
            .Where(i =>
                i.GameId == gameId &&
                i.IsCurrent &&
                !i.IsRemoved)
            .ToListAsync();

        foreach (var issue in issues)
        {
            issue.IsCurrent = false;
        }
    }

    public async Task<bool> ExistsByUrlAsync(Guid gameId, string url)
    {
        return await _dbSet.AnyAsync(i =>
            i.GameId == gameId &&
            i.Url == url &&
            !i.IsRemoved);
    }
}