using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Repositories;

public interface IGameRepository : IBaseRepository<Game>
{
    Task<bool> ExistsByNameAsync(string name, Guid createdBy);
    Task<bool> ExistsByNameAsync(string name, Guid createdBy, Guid excludedGameId);
    Task<bool> ExistsByInviteCodeAsync(string inviteCode);
    Task<Game?> GetByInviteCodeAsync(string inviteCode);
    Task<IEnumerable<Game>?> GetCreatedByUserId(Guid currentUserId);
    Task<IEnumerable<Game>?> GetAllByUserId(Guid currentUserId);
    Task<IEnumerable<Game>?> GetByUserIdParticipated(Guid currentUserId);
}
