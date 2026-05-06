using PlanningPoker.Domain.Models;
using Microsoft.Extensions.Configuration;
using PlanningPoker.BLL.DTOs.Issue;

namespace PlanningPoker.BLL.DTOs.Plane;

/// <summary>
/// Параметри імпорту задач із Plane.
/// </summary>
public class ImportPlaneIssuesDto
{
    /// <summary>
    /// Індифікатор проєкту Plane
    /// </summary>
    public string ProjectUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}


