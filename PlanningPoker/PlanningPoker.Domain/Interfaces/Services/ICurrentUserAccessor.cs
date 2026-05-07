namespace PlanningPoker.Domain.Interfaces.Services;

public interface ICurrentUserAccessor
{
    Guid? GetUserIdOrDefault();
    Guid? GetGuestParticipantIdOrDefault();
    Guid GetRequiredUserId();
}
