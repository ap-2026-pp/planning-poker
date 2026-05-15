using PlanningPoker.Domain.DTOs.Timer;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface ITimerService
{
    Task<TimerDto> StartTimerAsync(Guid gameId, int durationSeconds);
    Task StopTimerAsync(Guid gameId, bool requireMasterAccess = true);
    Task<TimerDto?> GetActiveTimerAsync(Guid gameId);
}
