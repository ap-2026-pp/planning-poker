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

    public async Task<GameParticipant?> GetCurrentParticipantAsync(Guid gameId, Guid? userId, Guid? guestParticipantId)
    {
        return await GetCurrentParticipantQuery(gameId, userId, guestParticipantId, includeRemoved: false)
            .FirstOrDefaultAsync();
    }

    public async Task<GameParticipant?> GetCurrentParticipantIncludingRemovedAsync(Guid gameId, Guid? userId, Guid? guestParticipantId)
    {
        return await GetCurrentParticipantQuery(gameId, userId, guestParticipantId, includeRemoved: true)
            .FirstOrDefaultAsync();
    }

    private IQueryable<GameParticipant> GetCurrentParticipantQuery(
        Guid gameId,
        Guid? userId,
        Guid? guestParticipantId,
        bool includeRemoved)
    {
        var query = _dbSet
            .Include(participant => participant.Game)
            .Where(participant => participant.GameId == gameId);

        if (!includeRemoved)
        {
            query = query.Where(participant => participant.RemovedAt == null);
        }

        if (userId.HasValue)
        {
            return query.Where(participant => participant.UserId == userId.Value);
        }

        if (guestParticipantId.HasValue)
        {
            return query.Where(participant =>
                participant.Id == guestParticipantId.Value &&
                participant.UserId == null);
        }

        return _dbSet.Where(_ => false);
    }

    public void RemoveGameParticipant(GameParticipant participant)
    {
        participant.IsConnected = false;
        participant.RemovedAt = DateTime.UtcNow;
        _dbSet.Update(participant);
    } 

    public void MarkParticipantOffline(GameParticipant participant)
    {
        participant.IsConnected = false;
        participant.RemovedAt = null;
        _dbSet.Update(participant);
    }

    public async Task<GameParticipant?> GetByGameAndUserAsync(Guid gameId, Guid userId)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.GameId == gameId && x.UserId == userId);
    }

    public async Task<bool> ExistsByDisplayNameAsync(string? displayName, Guid gameId)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        return await _dbSet.AnyAsync(participant =>
            participant.DisplayName == displayName &&
            participant.GameId == gameId &&
            participant.RemovedAt == null);
    }
    
    public async Task<IReadOnlyCollection<Guid>> GetAuthorizedUserIdsByGameIdAsync(Guid gameId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(participant => participant.GameId == gameId && participant.UserId != null)
            .Select(participant => participant.UserId!.Value)
            .Distinct()
            .ToListAsync();
    }
}
