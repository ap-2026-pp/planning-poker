namespace PlanningPoker.Domain.Interfaces.Services;

public interface ICurrentUserAccessor
{
    Guid GetRequiredUserId();
}
