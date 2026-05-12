namespace PlanningPoker.Domain.DTOs.Game;

/// <summary>
/// DTO для масового оновлення прав учасників гри.
/// Використовується в налаштуваннях гри (Game Settings).
/// </summary>
public class UpdateBulkPermissionsRequestDto
{
    public List<ParticipantPermissionsDto> Participants { get; set; } = new();
}