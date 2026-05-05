using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IGuestAccountLinkService
{
    Task AttachGuestParticipantToUserAsync(User user);
}
