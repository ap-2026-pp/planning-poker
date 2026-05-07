using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Виконує просту прив'язку поточного guest participant до авторизованого користувача
/// після успішного логіну або реєстрації.
/// </summary>
public class GuestAccountLinkService(
    ICurrentUserContext currentUserContext,
    IParticipantRepository participantRepository,
    IGuestSessionService guestSessionService) : IGuestAccountLinkService
{
    
    /// <summary>
    /// Прив'язує поточного гостьового учасника до користувача.
    /// Якщо в цій самій грі вже існує окремий participant для цього користувача,
    /// метод пропускає прив'язку, щоб не створювати конфлікт без merge-сценарію.
    /// Після успішної прив'язки всі активні guest session цього participant відкликаються.
    /// </summary>
    /// <param name="user">Користувач, до якого потрібно прив'язати guest participant.</param>
    public async Task AttachGuestParticipantToUserAsync(User user)
    {
        var currentIdentity = currentUserContext.GetCurrentParticipantIdentity();
        if (!currentIdentity.GuestParticipantId.HasValue || currentIdentity.UserId.HasValue)
        {
            return;
        }

        var participant = await participantRepository.GetByIdAsync(currentIdentity.GuestParticipantId.Value);
        if (participant is null || participant.UserId.HasValue)
        {
            return;
        }

        var existingUserParticipant = await participantRepository.GetByGameAndUserAsync(participant.GameId, user.Id);
        if (existingUserParticipant is not null && existingUserParticipant.Id != participant.Id)
        {
            return;
        }

        participant.UserId = user.Id;
        participantRepository.Update(participant);
        await participantRepository.SaveChangesAsync();
        await guestSessionService.RevokeGuestSessionsAsync(participant.Id);
    }
}
