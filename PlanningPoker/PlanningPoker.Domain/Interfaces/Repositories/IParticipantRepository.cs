using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Repositories;

public interface IParticipantRepository : IBaseRepository<GameParticipant>
{
    Task<IEnumerable<GameParticipant>?> GetGameParticipantsAsync(Guid gameId);
    Task<GameParticipant?> GetByUserIdAndGameIdAsync(Guid userId, Guid gameId);
    void DeleteGameParticipant(Guid gameId, Guid participantId);
}
