using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IParticipantService
{
    public Task<IEnumerable<GameParticipant>?> GetGameParticipantsAsync(Guid gameId);
    public Task DeleteGameParticipantAsync(Guid gameId, Guid participantId);
}
