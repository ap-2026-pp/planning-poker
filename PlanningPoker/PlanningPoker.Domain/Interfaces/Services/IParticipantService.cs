using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IParticipantService
{
    public Task<IEnumerable<GameParticipantDto>?> GetGameParticipantsAsync(Guid gameId);
    public Task DeleteGameParticipantAsync(Guid gameId, Guid participantId);
}
