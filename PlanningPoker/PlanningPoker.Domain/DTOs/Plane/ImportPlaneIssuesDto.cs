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
    /// Slug робочого простору Plane.
    /// </summary>
    public string WorkspaceSlug { get; set; } = string.Empty;
    /// <summary>
    /// Індифікатор проєкту Plane
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;
}


