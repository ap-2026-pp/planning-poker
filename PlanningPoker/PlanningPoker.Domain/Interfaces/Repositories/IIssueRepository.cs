using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Repositories;

public interface IIssueRepository : IBaseRepository<Issue>
{
    Task<IEnumerable<Issue>> GetByGameIdAsync(Guid gameId);

    Task<Issue?> GetByGameAndIssueAsync(Guid gameId, Guid issueId);

    Task<int> GetNextOrderAsync(Guid gameId);

    Task<IEnumerable<Issue>> GetIssuesByIdsAsync(Guid gameId, List<Guid> issueIds);

    Task ClearCurrentIssueAsync(Guid gameId);

    Task<bool> ExistsByUrlAsync(Guid gameId, string url);
    Task<string> GenerateIssueCodeAsync(Guid gameId);
    Task<Issue?> GetLastCreatedIssueAsync(Guid gameId);
    Task<IEnumerable<Issue>> GetByGameIdWithVotingResultsAsync(Guid gameId);
    Task<Issue?> GetActiveIssueByGameIdAsync(Guid gameId);
    Task<Issue?> GetByUrlAsync(Guid gameId, string url);
    
    Task<Issue?> GetByPlaneIssueIdAsync(Guid gameId, string planeIssueId);
}