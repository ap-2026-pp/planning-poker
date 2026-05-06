namespace PlanningPoker.BLL.DTOs.Plane;

/// <summary>
/// Отримана задача з Plane
/// </summary>
public class PlaneIssueDto
{
    public string Id { get; set; } 
    public int? SequenceId { get; set;}
    public string Name { get; set; } = string.Empty;
    public string? DescriptionHtml { get; set; }
    public string Status { get; set;}
    public DateTime CreatedAt { get; set; }
}

