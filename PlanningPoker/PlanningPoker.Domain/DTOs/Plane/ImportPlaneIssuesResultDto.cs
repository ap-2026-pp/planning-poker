namespace PlanningPoker.BLL.DTOs.Plane;
using PlanningPoker.BLL.DTOs.Issue;

/// <summary>
/// Імпортує задачі з Plane у список задач 
/// </summary>
public class ImportPlaneIssuesResultDto
{
    public int ImportCount { get; set; }
    public int SkippedCount { get; set; }
    public List<IssueDto> ImportedIssue { get; set;} = new();
}