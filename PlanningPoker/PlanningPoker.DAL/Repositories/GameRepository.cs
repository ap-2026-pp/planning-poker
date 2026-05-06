using Microsoft.EntityFrameworkCore;
using PlanningPoker.DAL.Data;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Repositories;

internal class GameRepository(AppDbContext context) : BaseRepository<Game>(context), IGameRepository
{
    public async Task<bool> ExistsByNameAsync(string name, Guid createdBy)
    {
        return await _dbSet.AnyAsync(g => g.Name == name && g.CreatedBy == createdBy && !g.IsDeleted);
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid createdBy, Guid excludedGameId)
    {
        return await _dbSet.AnyAsync(g =>
            g.Name == name &&
            g.CreatedBy == createdBy &&
            g.Id != excludedGameId && !g.IsDeleted);
    }

    public async Task<bool> ExistsByInviteCodeAsync(string inviteCode)
    {
        return await _dbSet.AnyAsync(g => g.InviteCode == inviteCode && !g.IsDeleted);
    }
    
    public new async Task<Game?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(g => g.Participants.Where(participant => participant.RemovedAt == null))
            .Include(g => g.Issues.Where(issue => !issue.IsRemoved))
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<Game?> GetByInviteCodeAsync(string inviteCode)
    {
        return await _dbSet
            .Include(g => g.Participants.Where(participant => participant.RemovedAt == null))
            .Include(g => g.Issues.Where(issue => !issue.IsRemoved))
            .FirstOrDefaultAsync(g => g.InviteCode == inviteCode && !g.IsDeleted);
    }

    public async Task<IEnumerable<Game>?> GetByUserId(Guid currentUserId)
    {
        return await GetUserGamesQuery()
            .Where(g => g.CreatedBy == currentUserId && !g.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Game>?> GetAllByUserId(Guid currentUserId)
    {
        return await GetUserGamesQuery()
            .Where(g =>
                !g.IsDeleted &&
                (g.CreatedBy == currentUserId ||
                 g.Participants.Any(participant =>
                     participant.UserId == currentUserId &&
                     participant.RemovedAt == null)))
            .ToListAsync();
    }

    public async Task<IEnumerable<Game>?> GetByUserIdParticipated(Guid currentUserId)
    {
        return await GetUserGamesQuery()
            .Where(g =>
                g.CreatedBy != currentUserId &&
                !g.IsDeleted &&
                g.Participants.Any(participant =>
                    participant.UserId == currentUserId &&
                    participant.RemovedAt == null))
            .ToListAsync();
    }

    private IQueryable<Game> GetUserGamesQuery()
    {
        return _dbSet
            .AsNoTracking()
            .Include(g => g.Participants.Where(participant => participant.RemovedAt == null))
            .Include(g => g.Issues.Where(issue => !issue.IsRemoved));
    }
}
