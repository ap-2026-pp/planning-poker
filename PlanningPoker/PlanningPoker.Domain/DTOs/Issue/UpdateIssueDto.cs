namespace PlanningPoker.BLL.DTOs.Issue;

/// <summary>
/// Містить дані для оновлення задачі
/// </summary>
public class UpdateIssueDto
{
    /// <summary>
    /// Код задачі
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Назва задачі
    /// </summary>
    public string Title { get; set; } 

    /// <summary>
    /// Посилання на задачу у зовнішній системі
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Опис задачі
    /// </summary>
    public string? Description { get; set; }
}

