using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface ICurrentUserContext
{
    Guid GetRequiredUserId();
    Task<User> GetRequiredUserAsync();
}
