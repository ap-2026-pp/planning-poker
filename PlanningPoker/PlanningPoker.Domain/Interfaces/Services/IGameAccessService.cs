using PlanningPoker.Domain.Models;
using PlanningPoker.Domain.DTOs.Game;
namespace PlanningPoker.Domain.Interfaces.Services;

public interface IGameAccessService
{
    Task<GameParticipant> GetRequiredParticipantAsync(Guid gameId);
    Task<GameParticipant> GetRequiredMasterAsync(Guid gameId);
    Task<GameParticipant> EnsureCanManageIssuesAsync(Guid gameId);
    Task<GameParticipant> EnsureCanRevealCardsAsync(Guid gameId);
    Task<GameParticipant> EnsureCanVoteAsync(Guid gameId);
    Task UpdateBulkPermissionsAsync(Guid gameId, UpdateBulkPermissionsRequestDto dto);
}