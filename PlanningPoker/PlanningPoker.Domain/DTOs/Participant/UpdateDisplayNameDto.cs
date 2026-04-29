using System.ComponentModel.DataAnnotations;

namespace PlanningPoker.Domain.DTOs.Participant;

public class UpdateDisplayNameDto
{
    [MaxLength(200)]
    public string? DisplayName { get; set; }
}
