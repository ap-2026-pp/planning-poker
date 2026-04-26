using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

public class ParticipantService(
    IParticipantRepository participantRepository,
    ICurrentUserService currentUserService) : IParticipantService
{
    public async Task<IEnumerable<GameParticipant>?> GetGameParticipantsAsync(Guid gameId)
    {
        return await participantRepository.GetGameParticipantsAsync(gameId);
    }

    public async Task DeleteGameParticipantAsync(Guid gameId, Guid participantId)
    {
        var currentUserId = currentUserService.GetRequiredUserId();
        var currentUserParticipant = await participantRepository.GetByUserIdAndGameIdAsync(currentUserId, gameId);
        if (currentUserParticipant is null || currentUserParticipant.Role != Role.Master)
        {
            throw new ForbiddenException("delete", "participant");
        }
        
        var participant = await participantRepository.GetByIdAsync(participantId);
        if (participant is null || participant.GameId != gameId)
        {
            throw new NotFoundException(nameof(GameParticipant), participantId);
        }
        
        participantRepository.DeleteGameParticipant(gameId, participantId);
        await participantRepository.SaveChangesAsync();
    }
}
