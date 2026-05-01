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
    public string Code {get; set;} 

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
    /// Визначає, чи заадча є поточною для голосування
    /// </summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Фінальна оцінка задачі
    /// </summary>
    public string? FinalEstimate { get; set; } 
}





