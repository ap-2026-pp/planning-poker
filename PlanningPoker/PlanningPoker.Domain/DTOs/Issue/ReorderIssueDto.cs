namespace PlanningPoker.BLL.DTOs.Issue;
/// <summary>
/// Поточний порядок задач у списку
/// </summary>
public class ReorderIssueDto
{
    public List<Guid> IssuesIds { get; set; } = new();
}

