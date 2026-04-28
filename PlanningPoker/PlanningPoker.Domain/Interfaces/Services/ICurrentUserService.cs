namespace PlanningPoker.Domain.Interfaces.Services;

public interface ICurrentUserService
{
    Guid GetRequiredUserId();
}
