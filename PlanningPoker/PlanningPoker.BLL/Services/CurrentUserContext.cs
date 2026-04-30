using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Надає доступ до поточного користувача на рівні бізнес-логіки.
/// </summary>
public class CurrentUserContext(
    ICurrentUserAccessor currentUserAccessor,
    IUserRepository userRepository) : ICurrentUserContext
{
    /// <summary>
    /// Повертає ідентифікатор поточного автентифікованого користувача.
    /// </summary>
    public Guid GetRequiredUserId()
    {
        return currentUserAccessor.GetRequiredUserId();
    }

    /// <summary>
    /// Повертає поточного користувача або викидає виняток, якщо його не знайдено.
    /// </summary>
    public async Task<User> GetRequiredUserAsync()
    {
        var userId = GetRequiredUserId();

        return await userRepository.GetByIdAsync(userId)
               ?? throw new NotFoundException(nameof(User), userId);
    }
}
