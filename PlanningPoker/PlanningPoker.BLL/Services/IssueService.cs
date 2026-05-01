using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.BLL.DTOs.Plane;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Сервіс для управління задачами (Issues) у межах ігрової сесії.
/// </summary>
public class IssueService(
    IIssueRepository repoIssues,
    IPlaneService planeService,
    IGameAccessService gameAccessService) : IIssueService
{
    /// <summary>
    /// Отримує список усіх активних задач для конкретної гри.
    /// </summary>
    public async Task<IEnumerable<IssueDto>> GetIssuesByGameAsync(Guid gameId)
    {
        await gameAccessService.GetRequiredParticipantAsync(gameId);

        var issues = await repoIssues.GetByGameIdAsync(gameId);
        return issues.Select(IssueMapper.ToDto);
    }

    /// <summary>
    /// Отримує детальну інформацію про конкретну задачу.
    /// </summary>
    public async Task<IssueDetailsDto> GetIssueByIdAsync(Guid gameId, Guid issueId)
    {
        await gameAccessService.GetRequiredParticipantAsync(gameId);

        var issue = await repoIssues.GetByGameAndIssueAsync(gameId, issueId)
                    ?? throw new NotFoundException(nameof(Issue), issueId);

        return IssueMapper.ToDetailsDto(issue);
    }

    /// <summary>
    /// Створює нову задачу в грі, доступно лише Майстру.
    /// </summary>
    public async Task<IssueDto> CreateIssueAsync(Guid gameId, CreateIssueDto dto)
    {
        var participant = await gameAccessService.EnsureCanManageIssuesAsync(gameId);
        var order = await repoIssues.GetNextOrderAsync(gameId);

        var issue = new Issue
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Url = string.Empty,
            Title = dto.Title.Trim(),
            Description = string.Empty,
            Order = order,
            IsCurrent = false,
            IsRemoved = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = participant.UserId 
        };

        await repoIssues.AddAsync(issue);
        await repoIssues.SaveChangesAsync();

        return IssueMapper.ToDto(issue);
    }

    /// <summary>
    /// Оновлює дані існуючої задачі. Забороняє зміну заголовка для задач із Plane.
    /// Перевіряємо, чи користувач намагається змінити заголовок,
    /// для локальних задач дозволено все
    /// </summary>
    public async Task<IssueDto> UpdateIssueAsync(Guid gameId, Guid issueId, UpdateIssueDto dto)
    {
        await gameAccessService.EnsureCanManageIssuesAsync(gameId);

        var issue = await repoIssues.GetByGameAndIssueAsync(gameId, issueId)
                    ?? throw new NotFoundException(nameof(Issue), issueId);

        bool isImportedFromPlane = !string.IsNullOrWhiteSpace(issue.Url);

        if (isImportedFromPlane)
        {
            if (issue.Title != dto.Title.Trim())
            {
                throw new ForbiddenException("Ви не маєте права змінювати заголовок задачі, що була імпортована з Plane.");
            }
            issue.Description = dto.Description?.Trim() ?? string.Empty;
        }
        else
        {
            issue.Title = dto.Title.Trim();
            issue.Description = dto.Description?.Trim() ?? string.Empty;
            issue.Url = dto.Url?.Trim() ?? string.Empty;
        }
        
        repoIssues.Update(issue);
        await repoIssues.SaveChangesAsync();

        return IssueMapper.ToDto(issue);
    }

    /// <summary>
    /// Позначає задачу як видалену (Soft delete),
    /// якщо видаляємо поточну активну задачу — скидаємо статус
    /// </summary>
    public async Task DeleteIssueAsync(Guid gameId, Guid issueId)
    {
        await gameAccessService.EnsureCanManageIssuesAsync(gameId);

        var issue = await repoIssues.GetByGameAndIssueAsync(gameId, issueId)
                    ?? throw new NotFoundException(nameof(Issue), issueId);

        issue.IsRemoved = true;
        
        if (issue.IsCurrent) issue.IsCurrent = false;

        repoIssues.Update(issue);
        await repoIssues.SaveChangesAsync();
    }

    /// <summary>
    /// Змінює порядок задач у списку.
    /// </summary>
    public async Task ReorderIssuesAsync(Guid gameId, ReorderIssueDto dto)
    {
        await gameAccessService.EnsureCanManageIssuesAsync(gameId);

        var issues = await repoIssues.GetIssuesByIdsAsync(gameId, dto.IssuesIds);
        var issuesList = issues.ToList();

        if (issuesList.Count != dto.IssuesIds.Count)
            throw new InvalidOperationException("Список задач невалідний або містить задачі з іншої гри");

        for (var i = 0; i < dto.IssuesIds.Count; i++)
        {
            var issue = issuesList.First(x => x.Id == dto.IssuesIds[i]);
            issue.Order = i + 1;
        }

        await repoIssues.SaveChangesAsync();
    }

    /// <summary>
    /// Встановлює задачу як активну для поточного голосування.
    /// </summary>
    public async Task<IssueDto> SetIssueActiveAsync(Guid gameId, Guid issueId)
    {
        await gameAccessService.EnsureCanManageIssuesAsync(gameId);

        var issue = await repoIssues.GetByGameAndIssueAsync(gameId, issueId)
                    ?? throw new NotFoundException(nameof(Issue), issueId);

        await repoIssues.ClearCurrentIssueAsync(gameId);

        issue.IsCurrent = true;

        repoIssues.Update(issue);
        await repoIssues.SaveChangesAsync();

        return IssueMapper.ToDto(issue);
    }

    /// <summary>
    /// Імпортує задачі із зовнішньої системи Plane.
    /// </summary>
    public async Task<IEnumerable<IssueDto>> ImportIssueByPlaneAsync(Guid gameId, ImportPlaneIssuesDto dto)
    {
        var participant = await gameAccessService.EnsureCanManageIssuesAsync(gameId);
        var planeIssues = await planeService.GetIssuesAsync(dto);
        var nextOrder = await repoIssues.GetNextOrderAsync(gameId);

        var newlyImported = false;

        foreach (var planeIssue in planeIssues)
        {
            var planeUrl = BuildPlaneIssueUrl(dto.WorkspaceSlug, dto.ProjectId, planeIssue.Id);

            var alreadyExists = await repoIssues.ExistsByUrlAsync(gameId, planeUrl);
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
                CreatedBy = participant.UserId
            };

            await repoIssues.AddAsync(issue);
            newlyImported = true;
        }

        if (newlyImported)
            await repoIssues.SaveChangesAsync();

        var allIssues = await repoIssues.GetByGameIdAsync(gameId);
        return allIssues.Select(IssueMapper.ToDto);
    }

    private static string BuildPlaneIssueUrl(string workspaceSlug, string projectId, string planeIssueId)
    {
        return $"https://app.plane.so/{workspaceSlug}/projects/{projectId}/issues/{planeIssueId}";
    }
}