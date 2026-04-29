using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Repositories;

public interface IIssueRepository : IBaseRepository<Issue>
{
    Task<IEnumerable<Issue>> GetByGameIdAsync(Guid gameId);

    Task<Issue?> GetByGameAndIssueAsync(Guid gameId, Guid issueId);

    Task<Issue?> GetCurrentIssueAsync(Guid gameId);

    Task<int> GetNextOrderAsync(Guid gameId);

    Task<int> GetNextIssueNumberAsync(Guid gameId);

    Task<IEnumerable<Issue>> GetIssuesByIdsAsync(Guid gameId, List<Guid> issueIds);

    Task ClearCurrentIssueAsync(Guid gameId);

    Task<bool> ExistsByUrlAsync(Guid gameId, string url);
}