using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

public class CurrentUserContext(
    ICurrentUserAccessor currentUserAccessor,
    IUserRepository userRepository) : ICurrentUserContext
{
    public Guid GetRequiredUserId()
    {
        return currentUserAccessor.GetRequiredUserId();
    }

    public async Task<User> GetRequiredUserAsync()
    {
        var userId = GetRequiredUserId();
        return await userRepository.GetByIdAsync(userId)
               ?? throw new NotFoundException(nameof(User), userId);
    }
}
