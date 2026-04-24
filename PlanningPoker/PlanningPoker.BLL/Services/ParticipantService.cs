using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

public class ParticipantService(IParticipantRepository participantRepository) : IParticipantService
{
    public async Task<IEnumerable<GameParticipant>?> GetGameParticipantsAsync(Guid gameId)
    {
        return await participantRepository.GetGameParticipantsAsync(gameId);
    }

    public async Task DeleteGameParticipantAsync(Guid gameId, Guid participantId)
    {
        var participant = await participantRepository.GetByIdAsync(participantId);
        if (participant == null)
        {
            return;
        }
        participantRepository.DeleteGameParticipant(gameId, participantId);
        await participantRepository.SaveChangesAsync();
    }
}