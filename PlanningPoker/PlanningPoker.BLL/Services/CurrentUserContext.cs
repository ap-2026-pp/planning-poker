using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Надає бізнес-логіці доступ до поточної ідентичності запиту:
/// авторизованого користувача або гостьового учасника гри.
/// </summary>
public class CurrentUserContext(
    ICurrentUserAccessor currentUserAccessor,
    IUserRepository userRepository) : ICurrentUserContext
{
    /// <summary>
    /// Повертає поточну ідентичність учасника запиту.
    /// Може містити ідентифікатор авторизованого користувача,
    /// ідентифікатор гостьового учасника або обидва значення залежно від контексту.
    /// </summary>
    /// <returns>Об'єкт <see cref="CurrentParticipantIdentity"/> з даними поточної ідентичності.</returns>
    public CurrentParticipantIdentity GetCurrentParticipantIdentity()
    {
        return new CurrentParticipantIdentity(GetUserIdOrDefault(), GetGuestParticipantIdOrDefault());
    }

    /// <summary>
    /// Повертає ідентифікатор поточного авторизованого користувача.
    /// </summary>
    /// <returns>Ідентифікатор поточного користувача.</returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Виникає, якщо в поточному контексті запиту немає валідного ідентифікатора авторизованого користувача.
    /// </exception>
    public Guid GetRequiredUserId()
    {
        return currentUserAccessor.GetRequiredUserId();
    }

    /// <summary>
    /// Повертає поточного авторизованого користувача як доменну сутність.
    /// </summary>
    /// <returns>Сутність <see cref="User"/> поточного користувача.</returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Виникає, якщо в поточному контексті запиту немає валідного ідентифікатора користувача.
    /// </exception>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо користувача з поточним ідентифікатором не знайдено.
    /// </exception>
    public async Task<User> GetRequiredUserAsync()
    {
        var userId = GetRequiredUserId();
        return await GetUserOrDefaultAsync() ?? throw new NotFoundException(nameof(User), userId);
    }

    /// <summary>
    /// Повертає поточного авторизованого користувача, якщо він присутній у контексті запиту та існує в сховищі.
    /// Якщо користувач неавторизований або не знайдений, повертає <see langword="null"/>.
    /// </summary>
    /// <returns>
    /// Сутність <see cref="User"/>, якщо поточний користувач визначений і знайдений;
    /// інакше <see langword="null"/>.
    /// </returns>
    public async Task<User?> GetUserOrDefaultAsync()
    {
        var userId = GetUserIdOrDefault();
        if (!userId.HasValue)
        {
            return null;
        }

        return await userRepository.GetByIdAsync(userId.Value);
    }
    
    /// <summary>
    /// Повертає ідентифікатор поточного авторизованого користувача, якщо він присутній у контексті запиту.
    /// Якщо користувач неавторизований, повертає <see langword="null"/>.
    /// </summary>
    /// <returns>Ідентифікатор поточного користувача або <see langword="null"/>.</returns>
    private Guid? GetUserIdOrDefault()
    {
        return currentUserAccessor.GetUserIdOrDefault();
    }

    /// <summary>
    /// Повертає ідентифікатор поточного гостьового учасника гри, якщо він присутній у контексті запиту.
    /// Якщо гостьовий учасник не визначений, повертає <see langword="null"/>.
    /// </summary>
    /// <returns>Ідентифікатор гостьового учасника або <see langword="null"/>.</returns>
    private Guid? GetGuestParticipantIdOrDefault()
    {
        return currentUserAccessor.GetGuestParticipantIdOrDefault();
    }
}
