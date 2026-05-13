using PlanningPoker.Domain.Models;
using PlanningPoker.Domain.DTOs.Timer;
namespace PlanningPoker.Domain.Mappers;

public static class TimerMapper
{
    public static TimerDto ToDto(GameTimer timer)
    {
        var now = DateTime.UtcNow;
        var remaining = (timer.EndsAt - now).TotalSeconds;

        return new TimerDto
        {
            GameId = timer.GameId,
            StartedAt = timer.StartedAt,
            EndsAt = timer.EndsAt,
            RemainingSeconds = remaining > 0 ? remaining : 0,
            IsExpired = now >= timer.EndsAt
        };
    }
}