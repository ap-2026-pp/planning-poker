namespace PlanningPoker.Domain.Models;

public sealed record CurrentParticipantIdentity(Guid? UserId, Guid? GuestParticipantId)
{
    public bool IsAuthenticated => UserId.HasValue || GuestParticipantId.HasValue;
    public bool IsGuest => GuestParticipantId.HasValue && !UserId.HasValue;
}
