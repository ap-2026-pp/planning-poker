using  PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.DTOs.Vote;

/// <summary>
/// 
/// </summary>
public class RoundResultDto 
{
    public double? Average { get; set; } 
    public double? Agreement { get; set; } 
    public string FinalEstimate { get; set; } = string.Empty; 
}