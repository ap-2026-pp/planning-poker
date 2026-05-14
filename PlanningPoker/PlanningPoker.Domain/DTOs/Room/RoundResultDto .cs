namespace PlanningPoker.Domain.DTOs.Room;

public class RoundResultDto 
{
    public double? Average { get; set; } 
    public double? Agreement { get; set; } 
    public string FinalEstimate { get; set; } = string.Empty; 
}