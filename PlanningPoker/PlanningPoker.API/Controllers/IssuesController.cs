using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.BLL.DTOs.Issue.Export;
using PlanningPoker.BLL.DTOs.Plane;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.API.Controllers;

/// <summary>
/// Контролер для управління задачами (issues) в межах конкретної гри.
/// Всі операції прив’язані до gameId.
/// </summary>
[ApiController]
[Route("api/games/{gameId:guid}/issues")]
[Authorize]
public class IssuesController : ControllerBase
{
    private readonly IIssueService _issueService;

    public IssuesController(IIssueService issueService)
    {
        _issueService = issueService;
    }

    /// <summary>
    /// Отримати всі задачі гри.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetIssuesByGame([FromRoute] Guid gameId)
    {
        var issues = await _issueService.GetIssuesByGameAsync(gameId);
        return Ok(issues);
    }

    /// <summary>
    /// Отримати задачу за Id.
    /// </summary>
    [HttpGet("{issueId:guid}")]
    public async Task<IActionResult> GetIssueById(
        [FromRoute] Guid gameId,
        [FromRoute] Guid issueId)
    {
        var issue = await _issueService.GetIssueByIdAsync(gameId, issueId);
        return Ok(issue);
    }

    /// <summary>
    /// Створити нову задачу в межах гри.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateIssue(
        [FromRoute] Guid gameId,
        [FromBody] CreateIssueDto dto)
    {
        var issue = await _issueService.CreateIssueAsync(gameId, dto);

        return CreatedAtAction(
            nameof(GetIssueById),
            new { gameId, issueId = issue.Id },
            issue);
    }

    /// <summary>
    /// Оновити задачу.
    /// </summary>
    [HttpPut("{issueId:guid}")]
    public async Task<IActionResult> UpdateIssue(
        [FromRoute] Guid gameId,
        [FromRoute] Guid issueId,
        [FromBody] UpdateIssueDto dto)
    {
        var issue = await _issueService.UpdateIssueAsync(gameId, issueId, dto);
        return Ok(issue);
    }

    /// <summary>
    /// Видалити задачу.
    /// </summary>
    [HttpDelete("{issueId:guid}")]
    public async Task<IActionResult> DeleteIssue(
        [FromRoute] Guid gameId,
        [FromRoute] Guid issueId)
    {
        await _issueService.DeleteIssueAsync(gameId, issueId);
        return NoContent();
    }

    /// <summary>
    /// Перевпорядкувати задачі.
    /// </summary>
    [HttpPatch("reorder")]
    public async Task<IActionResult> ReorderIssues(
        [FromRoute] Guid gameId,
        [FromBody] ReorderIssueDto dto)
    {
        await _issueService.ReorderIssuesAsync(gameId, dto);
        return NoContent();
    }

    /// <summary>
    /// Зробити задачу активною для голосування.
    /// </summary>
    [HttpPatch("{issueId:guid}/set-active")]
    public async Task<IActionResult> SetActiveIssue(
        [FromRoute] Guid gameId,
        [FromRoute] Guid issueId)
    {
        var issue = await _issueService.SetIssueActiveAsync(gameId, issueId);
        return Ok(issue);
    }

    /// <summary>
    /// Імпорт задач із Plane.
    /// </summary>
    [HttpPost("import-plane")]
    public async Task<IActionResult> ImportIssuesFromPlane(
        [FromRoute] Guid gameId,
        [FromBody] ImportPlaneIssuesDto dto)
    {
        var issues = await _issueService.ImportIssueByPlaneAsync(gameId, dto);
        return Ok(issues);
    }

    /// <summary>
    /// Експорт задач у CSV файл.
    /// </summary>
    [HttpPost("export-csv")]
    public async Task<IActionResult> ExportIssues(
        [FromRoute] Guid gameId,
        [FromBody] ExportIssuesRequestDto dto)
    {
        var fileResult = await _issueService.ExportToCsvAsync(gameId, dto);

        return File(fileResult.Content, "text/csv", fileResult.FileName);
    }

    /// <summary>
    /// Видалити всі задачі гри.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteAllIssuesAsync([FromRoute] Guid gameId)
    {
        await _issueService.DeleteAllIssuesAsync(gameId);
        return NoContent();
    }
}