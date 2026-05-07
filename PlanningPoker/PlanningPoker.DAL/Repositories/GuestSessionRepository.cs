using Microsoft.EntityFrameworkCore;
using PlanningPoker.DAL.Data;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Repositories;

internal class GuestSessionRepository(AppDbContext context)
    : BaseRepository<GuestSession>(context), IGuestSessionRepository
{
    public async Task<GuestSession?> GetActiveByTokenHashAsync(string tokenHash, DateTime utcNow)
    {
        return await _dbSet.FirstOrDefaultAsync(session =>
            session.TokenHash == tokenHash &&
            !session.IsRevoked &&
            session.ExpiresAt > utcNow);
    }

    public async Task RevokeActiveByParticipantIdAsync(Guid participantId, DateTime utcNow)
    {
        var sessions = await _dbSet.Where(session =>
                session.ParticipantId == participantId &&
                !session.IsRevoked &&
                session.ExpiresAt > utcNow)
            .ToListAsync();

        foreach (var session in sessions)
        {
            session.IsRevoked = true;
        }
    }
}
