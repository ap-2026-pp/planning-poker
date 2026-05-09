using PlanningPoker.Domain.Models;
namespace PlanningPoker.Domain.Interfaces.Services;

public interface IGameAccessService
{
    Task<GameParticipant> GetRequiredParticipantAsync(Guid gameId, string action, string resourceName);
    Task<GameParticipant> GetRequiredMasterAsync(Guid gameId, string action, string resourceName);
    Task<GameParticipant> EnsureCanManageIssuesAsync(Guid gameId, string action, string resourceName);
    Task<GameParticipant> EnsureCanRevealCardsAsync(Guid gameId);
    Task<GameParticipant> EnsureCanVoteAsync(Guid gameId);
    Task<GameParticipant> GetRequiredActiveParticipantAsync(Guid gameId, Guid participantId);
    Task<IReadOnlyList<GameParticipant>> GetOtherActiveNonSpectatorParticipantsAsync(Game game, Guid excludedParticipantId);
    Task<GameParticipant> GetRequiredNonSpectatorParticipantAsync(
        Guid gameId,
        Guid participantId,
        string action,
        string resourceName);

    Task<GameParticipant?> GetParticipantOrEnsureOwnerAsync(Game game, string action, string resourceName);
    Task<GameParticipant?> EnsureCanUpdateGameAsync(Game game);
    Task EnsureCanDeleteGameAsync(Game game);
}
