namespace PlanningPoker.BLL.DTOs.Issue;
/// <summary>
/// Список та загальна загальна кількість задач
/// </summary>
public class IssuesListDto
{
    public int Count { get; set; }
    public List<IssueDto> Issues { get; set; } = new();
}