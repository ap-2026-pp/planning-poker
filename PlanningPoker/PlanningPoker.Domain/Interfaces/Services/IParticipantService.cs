using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IParticipantService
{
    public Task<IEnumerable<GameParticipantDto>> GetGameParticipantsAsync(Guid gameId);
    public Task<GameDto> JoinGameByInviteCodeAsync(Guid userId, string inviteCode, string? displayName);
    public Task LeaveGameAsync(Guid gameId, Guid userId);
    public Task DeleteGameParticipantAsync(Guid gameId, Guid userId, Guid participantId);
    public Task<GameParticipantDto> UpdateDisplayNameAsync(Guid gameId, Guid userId, string? displayName);
}
