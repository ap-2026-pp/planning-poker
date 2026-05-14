using System.ComponentModel.DataAnnotations;

namespace PlanningPoker.Domain.DTOs.Timer;

public class StartTimerRequestDto
{
    [Range(1, 3600)]
    public int DurationSeconds { get; set; }
}
