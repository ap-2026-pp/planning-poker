namespace PlanningPoker.Domain.Interfaces.Repositories;

using PlanningPoker.Domain.DTOs.VotingHistory;
using PlanningPoker.Domain.Models;

public interface IVotingHistoryRepository : IBaseRepository<VotingResult>
{
    Task<List<VotingResult>> GetHistoryRawAsync(Guid gameId);
    Task<VotingResult?> GetHistoryDetailsRawAsync(Guid gameId, Guid entryId);
}