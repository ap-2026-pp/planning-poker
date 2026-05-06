using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.BLL.DTOs.Plane;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Exceptions;

namespace PlanningPoker.BLL.Services;

internal class IssueService : IIssueService
{
    private readonly IIssueRepository _repoIssues;
    private readonly IParticipantRepository _repoParticipant;
    private readonly IPlaneService _planeService;

    public IssueService(
        IIssueRepository repoIssues,
        IPlaneService planeService,
        IParticipantRepository repoParticipant)
    {
        _repoIssues = repoIssues;
        _planeService = planeService;
        _repoParticipant = repoParticipant;
    }

    /// <summary>
    /// Повертає всі не видалені задачі конкретної гри
    /// </summary>
    public async Task<IEnumerable<IssueDto>> GetIssuesByGameAsync(Guid gameId)
    {
        var issues = await _repoIssues.GetByGameIdAsync(gameId);
        return issues.Select(IssueMapper.ToDto);
    }

    /// <summary>
    /// Повертає детальну інформацію про одну задачу гри
    /// </summary>
    public async Task<IssueDetailsDto> GetIssueByIdAsync(Guid gameId, Guid issueId)
    {
        var issue = await _repoIssues.GetByGameAndIssueAsync(gameId, issueId);

        if (issue is null)
            throw new Exception("Issue not found.");

        return IssueMapper.ToDetailsDto(issue);
    }

    /// <summary>
    /// Створює нову задачу в грі від імені учасника
    /// </summary>
    public async Task<IssueDto> CreateIssueAsync(Guid gameId,
        Guid userId,
        CreateIssueDto dto)
    {
        var participant = await _repoParticipant.GetByGameAndUserAsync(gameId, userId);
        if (participant == null)
            throw new Exception("User isn't participant of this game");

        if (participant.Role != ParticipantRole.Master)
            throw new Exception("Only master can create issues");
        var order = await _repoIssues.GetNextOrderAsync(gameId);

        var issue = new Issue
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Url = string.Empty,
            Title = dto.Title,
            Description = string.Empty,
            Order = order,
            IsCurrent = false,
            IsRemoved = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        await _repoIssues.AddAsync(issue);
        await _repoIssues.SaveChangesAsync();

        return IssueMapper.ToDto(issue);
    }

    /// <summary>
    /// Оновлює основні дані задачі: назву, посилання та опис
    /// </summary>
    public async Task<IssueDto> UpdateIssueAsync(Guid gameId,
        Guid issueId, UpdateIssueDto dto)
    {
        var issue = await _repoIssues.GetByGameAndIssueAsync(gameId, issueId);

        if (issue is null)
            throw new Exception("Issue not found");

        issue.Url = dto.Url ?? string.Empty;
        issue.Title = dto.Title;
        issue.Description = dto.Description ?? string.Empty;

        _repoIssues.Update(issue);
        await _repoIssues.SaveChangesAsync();

        return IssueMapper.ToDto(issue);
    }

    /// <summary>
    /// Позначає задачу як видалену 
    /// </summary>
    public async Task DeleteIssueAsync(Guid gameId, Guid issueId, Guid userId)
    {
        var issue = await _repoIssues.GetByGameAndIssueAsync(gameId, issueId);

        if (issue is null)
            throw new Exception("Issue not found.");

        issue.IsRemoved = true;

        _repoIssues.Update(issue);
        await _repoIssues.SaveChangesAsync();
    }

    /// <summary>
    /// Оновлює порядок задач у списку після перетягування
    /// </summary>
    public async Task ReorderIssuesAsync(Guid gameId, Guid userId, ReorderIssueDto dto)
    {
        var issues = await _repoIssues.GetIssuesByIdsAsync(gameId, dto.IssuesIds);
        var issuesList = issues.ToList();

        if (issuesList.Count != dto.IssuesIds.Count)
            throw new Exception("Invalid issues list.");

        for (var i = 0; i < dto.IssuesIds.Count; i++)
        {
            var issue = issuesList.First(x => x.Id == dto.IssuesIds[i]);
            issue.Order = i + 1;
        }

        await _repoIssues.SaveChangesAsync();
    }

    /// <summary>
    /// Робить задачу поточною для голосування в межах гри
    /// </summary>
    public async Task<IssueDto> SetIssueActiveAsync(Guid gameId, Guid issueId, Guid userId)
    {
        var issue = await _repoIssues.GetByGameAndIssueAsync(gameId, issueId);

        if (issue is null)
            throw new Exception("Issue not found.");

        await _repoIssues.ClearCurrentIssueAsync(gameId);

        issue.IsCurrent = true;

        _repoIssues.Update(issue);
        await _repoIssues.SaveChangesAsync();

        return IssueMapper.ToDto(issue);
    }

    /// <summary>
    /// Імпортує задачі, використовуючи лише посилання на проект та API ключ
    /// </summary>

    private (string workspaceSlug, string projectId) ParsePlaneUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidUrlException("Invalid Plane URL.");

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        var workspaceIndex = Array.IndexOf(segments, "projects");

        if (workspaceIndex <= 0 || workspaceIndex + 1 >= segments.Length)
            throw new InvalidUrlException("Invalid Plane URL structure.");

        var workspaceSlug = segments[0];
        var projectId = segments[workspaceIndex + 1];

        return (workspaceSlug, projectId);
    }

    internal static string BuildPlaneIssueUrl(string workspace, string project, string issueId)
        => $"https://app.plane.so/{workspace}/projects/{project}/issues/{issueId}";


    public async Task<IEnumerable<IssueDto>> ImportIssueByPlaneAsync(
        Guid gameId,
        Guid userId,
        ImportPlaneIssuesDto dto)
    {
        var participant = await _repoParticipant.GetByGameAndUserAsync(gameId, userId);

        if (participant?.Role != ParticipantRole.Master)
            throw new ForbiddenException("Access denied! Only Master can import issues.");

        var (workspaceSlug, projectId) = ParsePlaneUrl(dto.ProjectUrl);

        var planeIssues = await _planeService.GetIssuesAsync(workspaceSlug, projectId, dto.ApiKey);

        var nextOrder = await _repoIssues.GetNextOrderAsync(gameId);

        foreach (var planeIssue in planeIssues)
        {
            var planeUrl = BuildPlaneIssueUrl(workspaceSlug, projectId, planeIssue.Id);

            if (await _repoIssues.ExistsByUrlAsync(gameId, planeUrl))
                continue;

            var issue = new Issue
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                Url = planeUrl,
                Title = planeIssue.Name ?? "Untitled Issue",
                Description = planeIssue.DescriptionHtml ?? string.Empty,
                Order = nextOrder++,
                IsRemoved = false,
                IsCurrent = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = participant.Id
            };

            await _repoIssues.AddAsync(issue);
        }

        await _repoIssues.SaveChangesAsync();

        var allIssues = await _repoIssues.GetByGameIdAsync(gameId);
        return allIssues.Select(IssueMapper.ToDto);
    }
}