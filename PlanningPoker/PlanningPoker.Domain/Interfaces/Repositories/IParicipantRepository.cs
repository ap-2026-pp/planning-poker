namespace PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

public interface IParticipantRepository : IBaseRepository<GameParticipant>
{
    public Task<IEnumerable<GameParticipant>?> GetGameParticipantsAsync(Guid gameId);
    public Task<GameParticipant?> GetActiveByIdAsync(Guid participantId);

    public Task<GameParticipant?> GetByUserIdAndGameIdAsync(Guid userId, Guid gameId);

    public void RemoveGameParticipant(GameParticipant participant);
    public Task<GameParticipant?> GetByGameAndUserAsync(Guid gameId, Guid userId);
}

