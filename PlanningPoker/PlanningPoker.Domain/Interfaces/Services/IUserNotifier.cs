using PlanningPoker.Domain.DTOs.Game;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IUserNotifier
{
    Task NotifyGameUpdatedAsync(Guid userId, UserGameDto payload);
}