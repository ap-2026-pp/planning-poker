namespace PlanningPoker.Domain.DTOs.Game;

public class UpdateBulkPermissionsRequestDto
{
    public List<ParticipantPermissionsDto> Participants { get; set; } = new();
}