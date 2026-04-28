using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Repositories;

public interface IParticipantRepository : IBaseRepository<GameParticipant>
{
    Task<IEnumerable<GameParticipant>?> GetGameParticipantsAsync(Guid gameId);
    Task<GameParticipant?> GetActiveByIdAsync(Guid participantId);
    Task<GameParticipant?> GetByUserIdAndGameIdAsync(Guid userId, Guid gameId);
    void RemoveGameParticipant(GameParticipant participant);
}
