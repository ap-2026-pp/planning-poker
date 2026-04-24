using Microsoft.EntityFrameworkCore;
using PlanningPoker.DAL.Data;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Repositories;

public class ParticipantRepository(AppDbContext context) : BaseRepository<GameParticipant>(context), IParticipantRepository
{
    public async Task<IEnumerable<GameParticipant>?> GetGameParticipantsAsync(Guid gameId)
    {
        return await DbSet.Include(participant => participant.Votes)
            .Include(participant => participant.Issues)
            .Where(participant => participant.GameId == gameId)
            .ToListAsync();
    }

    public void DeleteGameParticipant(Guid gameId, Guid participantId)
    {
        DbSet.RemoveRange(DbSet.Where(participant => participant.GameId == gameId && participant.Id == participantId));
    } 
}