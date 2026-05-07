using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Repositories;

public interface IParticipantRepository : IBaseRepository<GameParticipant>
{
    Task<IEnumerable<GameParticipant>?> GetGameParticipantsAsync(Guid gameId);
    Task<GameParticipant?> GetActiveByIdAsync(Guid participantId);
    Task<GameParticipant?> GetCurrentParticipantAsync(Guid gameId, Guid? userId, Guid? guestParticipantId);
    Task<GameParticipant?> GetCurrentParticipantIncludingRemovedAsync(Guid gameId, Guid? userId, Guid? guestParticipantId);
    Task<GameParticipant?> GetByGameAndUserAsync(Guid gameId, Guid userId);
    void RemoveGameParticipant(GameParticipant participant);
    Task<bool> ExistsByDisplayNameAsync(string? displayName, Guid gameId);
}
