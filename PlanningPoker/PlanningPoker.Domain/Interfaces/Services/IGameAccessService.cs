using PlanningPoker.Domain.Models;
namespace PlanningPoker.Domain.Interfaces.Services;

public interface IGameAccessService
{
    Task<GameParticipant> GetRequiredParticipantAsync(Guid gameId);
    Task<GameParticipant> GetRequiredMasterAsync(Guid gameId);
    Task<GameParticipant> EnsureCanManageIssuesAsync(Guid gameId);
    Task<GameParticipant> EnsureCanRevealCardsAsync(Guid gameId);
    Task<GameParticipant> EnsureCanVoteAsync(Guid gameId);
}