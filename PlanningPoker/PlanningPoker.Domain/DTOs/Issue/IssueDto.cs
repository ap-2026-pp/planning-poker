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
    public string Title { get; set; }

    /// <summary>
    /// Опис заадачи
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Позиція задачі у списку
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Визначає, чи заадча є поточною для 
    /// </summary>
    public bool IsCurrent { get; set; }
    
    public bool IsRemoved { get; set; }

    /// <summary>
    /// Фінальна оцінка задачі
    /// </summary>
    public string? FinalEstimate { get; set; } 
    public string? Status { get; set;}
}



