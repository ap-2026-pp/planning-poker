using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.DTOs.Issue;

/// <summary>
///  Містить фінальну оцінку задачі
/// </summary>
public class UpdateFinalEstimateDto
{
     public string FinalEstimate { get; set; } = string.Empty;
}
