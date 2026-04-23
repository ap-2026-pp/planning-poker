using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Repositories;

public interface IGameRepository : IBaseRepository<Game>
{
    Task<bool> ExistsByNameAsync(string name, Guid createdBy);
}
