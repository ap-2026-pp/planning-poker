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
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<Game?> GetByInviteCodeAsync(string inviteCode)
    {
        return await _dbSet
            .Include(g => g.Participants.Where(participant => participant.RemovedAt == null))
            .FirstOrDefaultAsync(g => g.InviteCode == inviteCode && !g.IsDeleted);
    }
}
