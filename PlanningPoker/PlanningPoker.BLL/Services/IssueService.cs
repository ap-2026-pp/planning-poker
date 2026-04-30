using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.BLL.DTOs.Plane;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;
using  PlanningPoker.Domain.Mappers;

namespace PlanningPoker.BLL.Services;

public class IssueService : IIssueService
{
    private readonly IIssueRepository _repoIssues;
    private readonly IParticipantRepository _repoParticipant;
    private readonly IPlaneService _planeService;
    private readonly ICurrentUserContext _currentUserContext;

    public IssueService(
        IIssueRepository repoIssues,
        IPlaneService planeService,
        IParticipantRepository repoParticipant,
        ICurrentUserContext currentUserContext)
    {
        _repoIssues = repoIssues;
        _planeService = planeService;
        _repoParticipant = repoParticipant;
        _currentUserContext = currentUserContext;
    }

    /// <summary>
    /// Повертає всі не видалені задачі конкретної гри
    /// </summary>
    public async Task<IEnumerable<IssueDto>> GetIssuesByGameAsync(Guid gameId)
    {
        var userId = _currentUserContext.GetRequiredUserId();
        await GetRequiredParticipantAsync(gameId, userId, "view", "issues");
        var issues = await _repoIssues.GetByGameIdAsync(gameId);
        return issues.Select(IssueMapper.ToDto);
    }

    /// <summary>
    /// Повертає детальну інформацію про одну задачу гри
    /// </summary>
    public async Task<IssueDetailsDto> GetIssueByIdAsync(Guid gameId, Guid issueId)
    {
        var userId = _currentUserContext.GetRequiredUserId();
        await GetRequiredParticipantAsync(gameId, userId, "view", "issue");
        var issue = await _repoIssues.GetByGameAndIssueAsync(gameId, issueId);

        if (issue is null)
            throw new NotFoundException(nameof(Issue), issueId);

        return IssueMapper.ToDetailsDto(issue);
    }

    /// <summary>
    /// Створює нову задачу в грі від імені учасника
    /// </summary>
    public async Task<IssueDto> CreateIssueAsync(Guid gameId, CreateIssueDto dto)
    {
        var userId = _currentUserContext.GetRequiredUserId();
        var participant = await GetRequiredMasterParticipantAsync(gameId, userId, "create", "issue");
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
            CreatedBy = participant.Id
        };

        await _repoIssues.AddAsync(issue);
        await _repoIssues.SaveChangesAsync();

        return IssueMapper.ToDto(issue);
    }

    /// <summary>
    /// Оновлює основні дані задачі: назву, посилання та опис
    /// </summary>
    public async Task<IssueDto> UpdateIssueAsync(Guid gameId, Guid issueId, UpdateIssueDto dto)
    {
        var userId = _currentUserContext.GetRequiredUserId();
        await GetRequiredMasterParticipantAsync(gameId, userId, "update", "issue");
        var issue = await _repoIssues.GetByGameAndIssueAsync(gameId, issueId);

        if (issue is null)
            throw new NotFoundException(nameof(Issue), issueId);

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
    public async Task DeleteIssueAsync(Guid gameId, Guid issueId)
    {
        var userId = _currentUserContext.GetRequiredUserId();
        await GetRequiredMasterParticipantAsync(gameId, userId, "delete", "issue");
        var issue = await _repoIssues.GetByGameAndIssueAsync(gameId, issueId);

        if (issue is null)
            throw new NotFoundException(nameof(Issue), issueId);

        issue.IsRemoved = true;

        _repoIssues.Update(issue);
        await _repoIssues.SaveChangesAsync();
    }

    /// <summary>
    /// Оновлює порядок задач у списку після перетягування
    /// </summary>
   public async Task ReorderIssuesAsync(Guid gameId, ReorderIssueDto dto)
    {
        var userId = _currentUserContext.GetRequiredUserId();
        await GetRequiredMasterParticipantAsync(gameId, userId, "reorder", "issues");
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
    public async Task<IssueDto> SetIssueActiveAsync(Guid gameId, Guid issueId)
    {
        var userId = _currentUserContext.GetRequiredUserId();
        await GetRequiredMasterParticipantAsync(gameId, userId, "set active", "issue");
        var issue = await _repoIssues.GetByGameAndIssueAsync(gameId, issueId);

        if (issue is null)
            throw new NotFoundException(nameof(Issue), issueId);

        await _repoIssues.ClearCurrentIssueAsync(gameId);

        issue.IsCurrent = true;

        _repoIssues.Update(issue);
        await _repoIssues.SaveChangesAsync();

        return IssueMapper.ToDto(issue);
    }
    
    /// <summary>
    /// Імпортує задачі з Plane та додає їх до списку задач гри
    /// </summary>
    public async Task<IEnumerable<IssueDto>> ImportIssueByPlaneAsync(Guid gameId, ImportPlaneIssuesDto dto)
    {
        var userId = _currentUserContext.GetRequiredUserId();
        var participant = await GetRequiredMasterParticipantAsync(gameId, userId, "import", "issues");
        var planeIssues = await _planeService.GetIssuesAsync(dto);
        var nextOrder = await _repoIssues.GetNextOrderAsync(gameId);

        var newlyImported = false;

        foreach (var planeIssue in planeIssues)
        {
            var planeUrl = BuildPlaneIssueUrl(dto.WorkspaceSlug, dto.ProjectId, planeIssue.Id);

            var alreadyExists = await _repoIssues.ExistsByUrlAsync(gameId, planeUrl);
            if (alreadyExists) continue;

            var issue = new Issue
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                Url = planeUrl,
                Title = planeIssue.Name,
                Description = planeIssue.DescriptionHtml ?? string.Empty,
                Order = nextOrder++,
                IsCurrent = false,
                IsRemoved = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = participant.Id
            };

            await _repoIssues.AddAsync(issue);
            newlyImported = true; 
        }

        if (newlyImported)
        {
            await _repoIssues.SaveChangesAsync();
        }

        var allIssues = await _repoIssues.GetByGameIdAsync(gameId);
        return allIssues.Select(IssueMapper.ToDto);
    }

    /// <summary>
    /// Формує посилання на задачу в Plane
    /// </summary>
    private static string BuildPlaneIssueUrl(
        string workspaceSlug,
        string projectId,
        string planeIssueId)
    {
        return $"https://app.plane.so/{workspaceSlug}/projects/{projectId}/issues/{planeIssueId}";
    }

    private async Task<GameParticipant> GetRequiredMasterParticipantAsync(
        Guid gameId,
        Guid userId,
        string action,
        string resourceName)
    {
        var participant = await GetRequiredParticipantAsync(gameId, userId, action, resourceName);

        if (participant.Role != ParticipantRole.Master)
        {
            throw new ForbiddenException(action, resourceName);
        }

        return participant;
    }

    private async Task<GameParticipant> GetRequiredParticipantAsync(
        Guid gameId,
        Guid userId,
        string action,
        string resourceName)
    {
        var participant = await _repoParticipant.GetByUserIdAndGameIdAsync(userId, gameId);

        if (participant is null)
        {
            throw new ForbiddenException(action, resourceName);
        }

        return participant;
    }
}
