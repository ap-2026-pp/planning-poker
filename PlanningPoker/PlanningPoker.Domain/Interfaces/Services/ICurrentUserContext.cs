using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface ICurrentUserContext
{
    CurrentParticipantIdentity GetCurrentParticipantIdentity();
    Guid GetRequiredUserId();
    Task<User> GetRequiredUserAsync();
    Task<User?> GetUserOrDefaultAsync();
}
