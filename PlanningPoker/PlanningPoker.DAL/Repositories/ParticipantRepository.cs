using Microsoft.EntityFrameworkCore;
using PlanningPoker.DAL.Data;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Repositories;

public class ParticipantRepository(AppDbContext context) : BaseRepository<GameParticipant>(context), IParticipantRepository
{
    public async Task<IEnumerable<GameParticipant>?> GetGameParticipantsAsync(Guid gameId)
    {
        return await _dbSet.Include(participant => participant.Votes)
            .Include(participant => participant.Issues)
            .Where(participant => participant.GameId == gameId)
            .ToListAsync();
    }

    public async Task<GameParticipant?> GetByUserIdAndGameIdAsync(Guid userId, Guid gameId)
    {
        return await _dbSet.FirstOrDefaultAsync(participant =>
            participant.UserId == userId &&
            participant.GameId == gameId);
    }

    public void DeleteGameParticipant(Guid gameId, Guid participantId)
    {
        _dbSet.RemoveRange(_dbSet.Where(participant => participant.GameId == gameId && participant.Id == participantId));
    } 
}
