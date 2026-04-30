using Microsoft.EntityFrameworkCore;
using PlanningPoker.DAL.Data;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Repositories;

internal class ParticipantRepository(AppDbContext context) : BaseRepository<GameParticipant>(context), IParticipantRepository
{
    public async Task<IEnumerable<GameParticipant>?> GetGameParticipantsAsync(Guid gameId)
    {
        return await _dbSet.Include(participant => participant.Votes)
            .Include(participant => participant.Issues)
            .Where(participant => participant.GameId == gameId && participant.RemovedAt == null)
            .ToListAsync();
    }

    public async Task<GameParticipant?> GetActiveByIdAsync(Guid participantId)
    {
        return await _dbSet.FirstOrDefaultAsync(participant =>
            participant.Id == participantId &&
            participant.RemovedAt == null);
    }

    public async Task<GameParticipant?> GetByUserIdAndGameIdAsync(Guid userId, Guid gameId)
    {
        return await _dbSet.FirstOrDefaultAsync(participant =>
            participant.UserId == userId &&
            participant.GameId == gameId &&
            participant.RemovedAt == null);
    }

    public async Task<GameParticipant?> GetByUserIdAndGameIdIncludingRemovedAsync(Guid userId, Guid gameId)
    {
        return await _dbSet.FirstOrDefaultAsync(participant =>
            participant.UserId == userId &&
            participant.GameId == gameId);
    }

    public void RemoveGameParticipant(GameParticipant participant)
    {
        participant.IsConnected = false;
        participant.RemovedAt = DateTime.UtcNow;
        _dbSet.Update(participant);
    } 

    public async Task<GameParticipant?> GetByGameAndUserAsync(Guid gameId, Guid userId)
    {
        return _dbSet.FirstOrDefault(x => x.GameId == gameId && x.UserId == userId);
    }
    }

    public async Task<bool> ExistsByDisplayNameAsync(string displayName,  Guid gameId)
    {
        return await _dbSet.AnyAsync(participant =>
            participant.DisplayName == displayName &&
            participant.GameId == gameId &&
            participant.RemovedAt == null);
    }
}
