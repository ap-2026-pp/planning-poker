using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Repositories;

public interface IGuestSessionRepository : IBaseRepository<GuestSession>
{
    Task<GuestSession?> GetActiveByTokenHashAsync(string tokenHash, DateTime utcNow);
    Task RevokeActiveByParticipantIdAsync(Guid participantId, DateTime utcNow);
}
