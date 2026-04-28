using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

public class ParticipantService(
    IParticipantRepository participantRepository,
    ICurrentUserService currentUserService) : IParticipantService
{
    public async Task<IEnumerable<GameParticipantDto>?> GetGameParticipantsAsync(Guid gameId)
    {
        var participants = await participantRepository.GetGameParticipantsAsync(gameId);
        return (participants ?? []).Select(ParticipantMapper.ToGameParticipantDto);
    }

    public async Task DeleteGameParticipantAsync(Guid gameId, Guid participantId)
    {
        var currentUserId = currentUserService.GetRequiredUserId();
        var currentUserParticipant = await participantRepository.GetByUserIdAndGameIdAsync(currentUserId, gameId);
        if (currentUserParticipant is null || currentUserParticipant.Role != ParticipantRole.Master)
        {
            throw new ForbiddenException("delete", "participant");
        }
        
        var participant = await participantRepository.GetActiveByIdAsync(participantId);
        if (participant is null || participant.GameId != gameId)
        {
            throw new NotFoundException(nameof(GameParticipant), participantId);
        }
        
        participantRepository.RemoveGameParticipant(participant);
        await participantRepository.SaveChangesAsync();
    }
}
