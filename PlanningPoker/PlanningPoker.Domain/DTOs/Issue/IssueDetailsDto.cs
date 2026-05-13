using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.DTOs.Issue;

/// <summary>
/// Детальна інформацію про задачу
/// </summary>
public class IssueDetailsDto
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
    /// Посилання на задачу у зовнішній системі
    /// </summary>

    public string  Url { get; set; } = string.Empty;

    /// <summary>
    /// Назва задачі
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Опис задачі
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Фінальна оцінка задачі
    /// </summary>
    public string? FinalEstimate { get; set; }

    public IssueStatus Status { get; set; }

    /// <summary>
    /// Визначає, чи задача є поточною для голосування
    /// </summary>
    public bool IsCurrent { get; set; }
}
