using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Mappers;

public static class ParticipantMapper
{
    public static GameParticipantDto ToGameParticipantDto(GameParticipant participant) =>
        new()
        {
            Id = participant.Id,
            UserId = participant.UserId,
            DisplayName = participant.DisplayName,
            Role = participant.Role,
            JoinedAt = participant.JoinedAt,
            IsConnected = participant.IsConnected
        };
}