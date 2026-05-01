namespace PlanningPoker.BLL.DTOs.Issue;

/// <summary>
/// Задача у списку задач гри
/// </summary>
public class IssueDto
{
    /// <summary>
    /// Ідентифікатор задачі
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Код задачі
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Посилання на задачу
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Назва задачі
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Опис задачі
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Позиція задачі у списку
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Визначає, чи задача є поточною для голосування
    /// </summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Фінальна оцінка задачі
    /// </summary>
    public string? FinalEstimate { get; set; }
}