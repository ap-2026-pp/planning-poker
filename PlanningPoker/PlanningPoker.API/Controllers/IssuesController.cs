using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.BLL.DTOs.Plane;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;
namespace PlanningPoker.API.Controllers;

/// <summary>
/// Керує задачами в межах конкретної гри.
/// </summary>
[ApiController]
[Route("api/games/{gameId:guid}/issues")]
[Authorize]
public class IssuesController : ControllerBase
{
    private readonly IIssueService _issueService;
    private readonly ICurrentUserAccessor _currentUser;

    public IssuesController(IIssueService issueService, ICurrentUserAccessor currentUser)
    {
        _issueService = issueService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Повертає список задач гри.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetIssuesByGame([FromRoute] Guid gameId)
    {
        var issues = await _issueService.GetIssuesByGameAsync(gameId);
        return Ok(issues);
    }

    /// <summary>
    /// Повертає детальну інформацію про задачу.
    /// </summary>
    [Authorize(Roles = nameof(ParticipantRole.Master))]
    [HttpGet("{issueId:guid}")]
    public async Task<IActionResult> GetIssueById(
        [FromRoute] Guid gameId,
        [FromRoute] Guid issueId)
    {
        var issue = await _issueService.GetIssueByIdAsync(gameId, issueId);
        return Ok(issue);
    }

    /// <summary>
    /// Створює нову задачу в грі
    /// </summary>
    [Authorize(Roles = nameof(ParticipantRole.Master))]
    [HttpPost]
    public async Task<IActionResult> CreateIssue([FromRoute] Guid gameId,
        [FromBody] CreateIssueDto dto)
    {
        var issue = await _issueService.CreateIssueAsync(gameId, _currentUser.GetRequiredUserId(), dto);
        return CreatedAtAction(
            nameof(GetIssueById),
            new { gameId, issueId = issue.Id },
            issue);
    }

    /// <summary>
    /// Оновлює дані задачі
    /// </summary>
    [Authorize(Roles = nameof(ParticipantRole.Master))]
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
    /// Видаляє задачу з гри
    /// </summary>
    [Authorize(Roles = nameof(ParticipantRole.Master))]
    [HttpDelete("{issueId:guid}")]
    public async Task<IActionResult> DeleteIssue(
        [FromRoute] Guid gameId,
        [FromRoute] Guid issueId)
    {
        await _issueService.DeleteIssueAsync(gameId, issueId, _currentUser.GetRequiredUserId());
        return NoContent();
    }

    /// <summary>
    /// Оновлює порядок задач у грі
    /// </summary>
    [Authorize(Roles = nameof(ParticipantRole.Master))]
    [HttpPatch("reorder")]
    public async Task<IActionResult> ReorderIssues(
        [FromRoute] Guid gameId,
        [FromBody] ReorderIssueDto dto)
    {
        await _issueService.ReorderIssuesAsync(gameId, _currentUser.GetRequiredUserId(),dto);
        return NoContent();
    }

    /// <summary>
    /// Робить задачу поточною для голосування
    /// </summary>
    [Authorize(Roles = nameof(ParticipantRole.Master))]
    [HttpPatch("{issueId:guid}/set-active")]
    public async Task<IActionResult> SetActiveIssue(
        [FromRoute] Guid gameId,
        [FromRoute] Guid issueId)
    {
        var issue = await _issueService.SetIssueActiveAsync(gameId, issueId, _currentUser.GetRequiredUserId());
        return Ok(issue);
    }

    /// <summary>
    /// Імпортує задачі з Plane
    /// </summary>
    [Authorize(Roles = nameof(ParticipantRole.Master))]
    [HttpPost("import-plane")]
    public async Task<IActionResult> ImportIssuesFromPlane(
    [FromRoute] Guid gameId,
    [FromBody] ImportPlaneIssuesDto dto)
    {
        var issues = await _issueService.ImportIssueByPlaneAsync(gameId, _currentUser.GetRequiredUserId(), dto);
        return Ok(issues);
    }
}
