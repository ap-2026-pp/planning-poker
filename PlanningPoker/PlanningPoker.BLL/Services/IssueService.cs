using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.BLL.DTOs.Issue.Export;
using PlanningPoker.BLL.DTOs.Plane;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;
using System.Text;
using PlanningPoker.BLL.Constants;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Сервіс для управління задачами (Issues) у межах ігрової сесії.
/// </summary>
public class IssueService(
    IGameRepository gameRepository,
    IIssueRepository repoIssues,
    IPlaneService planeService,
    IGameAccessService gameAccessService,
    IGameRealtimeService gameRealtimeService) : IIssueService
{
    /// <summary>
    /// Отримує список усіх активних задач для конкретної гри.
    /// </summary>
    public async Task<IEnumerable<IssueDto>> GetIssuesByGameAsync(Guid gameId)
    {
        await GetGameOrThrowAsync(gameId);
        await gameAccessService.GetRequiredParticipantAsync(
            gameId,
            AccessControlConstants.ViewAction,
            AccessControlConstants.IssuesResource);
        var issues = await repoIssues.GetByGameIdAsync(gameId);

        return issues.Select(IssueMapper.ToDto);
    }

    /// <summary>
    /// Отримує детальну інформацію про конкретну задачу.
    /// </summary>
    public async Task<IssueDetailsDto> GetIssueByIdAsync(Guid gameId, Guid issueId)
    {
        await GetGameOrThrowAsync(gameId);
        await gameAccessService.GetRequiredParticipantAsync(
            gameId,
            AccessControlConstants.ViewAction,
            AccessControlConstants.IssueResource);
        var issue = await repoIssues.GetByGameAndIssueAsync(gameId, issueId)
                    ?? throw new NotFoundException(nameof(Issue), issueId);

        return IssueMapper.ToDetailsDto(issue);
    }

    /// <summary>
    /// Створює нову задачу в грі, якщо поточному учаснику дозволено керувати issues згідно з політикою гри.
    /// </summary>
    public async Task<IssueDto> CreateIssueAsync(Guid gameId, CreateIssueDto dto)
    {
        var game = await GetGameOrThrowAsync(gameId);
        var participant = await gameAccessService.EnsureCanManageIssuesAsync(
            gameId,
            AccessControlConstants.CreateAction,
            AccessControlConstants.IssueResource);

        if (dto == null || string.IsNullOrWhiteSpace(dto.Title))
        {
            throw new InvalidOperationException("Title is required");
        }

        var order = await repoIssues.GetNextOrderAsync(gameId);
        var code = await GenerateIssueCodeAsync(gameId);

        var issue = new Issue
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Url = string.Empty,
            Code = code,
            Title = dto.Title.Trim(),
            Description = string.Empty,
            Order = order,
            IsCurrent = false,
            IsRemoved = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = participant.Id
        };

        await repoIssues.AddAsync(issue);
        await repoIssues.SaveChangesAsync();
        await gameRealtimeService.NotifyIssueAddedAsync(game, IssueMapper.ToDto(issue));

        return IssueMapper.ToDto(issue);
    }

    public async Task<IssueDto> UpdateIssueAsync(Guid gameId, Guid issueId, UpdateIssueDto dto)
    {
        var game = await GetGameOrThrowAsync(gameId);
        await gameAccessService.EnsureCanManageIssuesAsync(
            gameId,
            AccessControlConstants.UpdateAction,
            AccessControlConstants.IssueResource);

        var issue = await repoIssues.GetByGameAndIssueAsync(gameId, issueId)
                    ?? throw new NotFoundException(nameof(Issue), issueId);

        var isImportedFromPlane = !string.IsNullOrWhiteSpace(issue.Url);

        if (isImportedFromPlane)
        {
            if (issue.Title != dto.Title.Trim())
            {
                throw new ForbiddenException("You cannot update the title of the imported issue.");
            }

            issue.Description = dto.Description?.Trim() ?? string.Empty;
        }
        else
        {
            issue.Title = dto.Title.Trim();
            issue.Description = dto.Description?.Trim() ?? string.Empty;
            issue.Url = dto.Url?.Trim() ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            issue.Code = dto.Code?.Trim() ?? issue.Code;
        }

        repoIssues.Update(issue);
        await repoIssues.SaveChangesAsync();
        await gameRealtimeService.NotifyIssueUpdatedAsync(game, IssueMapper.ToDto(issue));

        return IssueMapper.ToDto(issue);
    }

    /// <summary>
    /// Позначає задачу як видалену (Soft delete),
    /// якщо видаляємо поточну активну задачу — скидаємо статус
    /// </summary>
    public async Task DeleteIssueAsync(Guid gameId, Guid issueId)
    {
        var game = await GetGameOrThrowAsync(gameId);
        await gameAccessService.EnsureCanManageIssuesAsync(
            gameId,
            AccessControlConstants.DeleteAction,
            AccessControlConstants.IssueResource);

        var issue = await repoIssues.GetByGameAndIssueAsync(gameId, issueId)
                    ?? throw new NotFoundException(nameof(Issue), issueId);

        issue.IsRemoved = true;

        if (issue.IsCurrent) issue.IsCurrent = false;

        repoIssues.Update(issue);
        await repoIssues.SaveChangesAsync();
        await gameRealtimeService.NotifyIssueUpdatedAsync(game, IssueMapper.ToDto(issue));
    }

    /// <summary>
    /// видаляємо всі задачі до однієї гри
    /// </summary>
    public async Task DeleteAllIssuesAsync(Guid gameId)
    {
        var game = await GetGameOrThrowAsync(gameId);
        await gameAccessService.EnsureCanManageIssuesAsync(
            gameId,
            AccessControlConstants.DeleteAction,
            AccessControlConstants.IssueResource);

        var issues = (await repoIssues.GetByGameIdAsync(gameId)).ToList();

        foreach (var issue in issues)
        {
            issue.IsRemoved = true;
            issue.IsCurrent = false;
            repoIssues.Update(issue);
        }

        await repoIssues.SaveChangesAsync();

        foreach (var issue in issues)
        {
            await gameRealtimeService.NotifyIssueUpdatedAsync(game, IssueMapper.ToDto(issue));
        }
    }

    /// <summary>
    /// Змінює порядок задач у списку.
    /// </summary>
    public async Task ReorderIssuesAsync(Guid gameId, ReorderIssueDto dto)
    {
        var game = await GetGameOrThrowAsync(gameId);
        await gameAccessService.EnsureCanManageIssuesAsync(
            gameId,
            AccessControlConstants.ReorderAction,
            AccessControlConstants.IssueResource);

        var issues = await repoIssues.GetIssuesByIdsAsync(gameId, dto.IssuesIds);
        var issuesList = issues.ToList();

        if (issuesList.Count != dto.IssuesIds.Count)
            throw new InvalidOperationException("Invalid issues list.");

        var changedIssues = new List<Issue>();

        for (var i = 0; i < dto.IssuesIds.Count; i++)
        {
            var issue = issuesList.First(x => x.Id == dto.IssuesIds[i]);
            var nextOrder = i + 1;

            if (issue.Order == nextOrder) continue;
            issue.Order = nextOrder;
            repoIssues.Update(issue);
            changedIssues.Add(issue);
        }

        await repoIssues.SaveChangesAsync();

        foreach (var issue in changedIssues.OrderBy(x => x.Order))
        {
            await gameRealtimeService.NotifyIssueUpdatedAsync(game, IssueMapper.ToDto(issue));
        }
    }

    public async Task<IssueDto> SetIssueActiveAsync(Guid gameId, Guid issueId)
    {
        var game = await GetGameOrThrowAsync(gameId);
        await gameAccessService.EnsureCanManageIssuesAsync(
            gameId,
            AccessControlConstants.SetAction,
            AccessControlConstants.IssueResource);
        var issue = await repoIssues.GetByGameAndIssueAsync(gameId, issueId)
                    ?? throw new NotFoundException(nameof(Issue), issueId);

        if (issue.IsCurrent)
        {
            issue.IsCurrent = false;
            repoIssues.Update(issue);
            await repoIssues.SaveChangesAsync();
            await gameRealtimeService.NotifyIssueUpdatedAsync(game, IssueMapper.ToDto(issue));

            return IssueMapper.ToDto(issue);
        }

        var currentIssues = await repoIssues.GetByGameIdAsync(gameId);
        var currentActiveIssues = currentIssues.Where(x => x.IsCurrent && x.Id != issue.Id).ToList();

        foreach (var currentIssue in currentActiveIssues)
        {
            currentIssue.IsCurrent = false;
            repoIssues.Update(currentIssue);
        }

        issue.IsCurrent = true;
        repoIssues.Update(issue);

        await repoIssues.SaveChangesAsync();

        foreach (var currentIssue in currentActiveIssues)
        {
            await gameRealtimeService.NotifyIssueUpdatedAsync(game, IssueMapper.ToDto(currentIssue));
        }

        await gameRealtimeService.NotifyIssueUpdatedAsync(game, IssueMapper.ToDto(issue));

        return IssueMapper.ToDto(issue);
    }

    /// <summary>
    /// Імпортує задачі із зовнішньої системи Plane.
    /// </summary>
    /// <summary>
    /// Імпортує задачі із зовнішньої системи Plane.
    /// Якщо задача вже була імпортована раніше, вона оновлюється.
    /// Якщо задачі ще немає — створюється нова з наступним PP-кодом.
    /// </summary>
    public async Task<IEnumerable<IssueDto>> ImportIssueByPlaneAsync(Guid gameId, ImportPlaneIssuesDto dto)
    {
        var game = await GetGameOrThrowAsync(gameId);

        var participant = await gameAccessService.EnsureCanManageIssuesAsync(
            gameId,
            AccessControlConstants.ImportAction,
            AccessControlConstants.IssueResource);

        var (workspaceSlug, projectId) = ParsePlaneUrl(dto.ProjectUrl);

        var planeIssues = await planeService.GetIssuesAsync(
            gameId,
            workspaceSlug,
            projectId,
            dto.ApiKey);

        var nextOrder = await repoIssues.GetNextOrderAsync(gameId);
        var nextCodeNumber = await GetNextIssueCodeNumberAsync(gameId);

        foreach (var planeIssue in planeIssues)
        {
            var planeUrl = BuildPlaneIssueUrl(workspaceSlug, projectId, planeIssue.Id);

            var existingIssue =
                await repoIssues.GetByUrlAsync(gameId, planeUrl)
                ?? await repoIssues.GetByPlaneIssueIdAsync(gameId, planeIssue.Id);

            if (existingIssue is not null)
            {
                existingIssue.Title = planeIssue.Name.Trim();
                existingIssue.Description = planeIssue.DescriptionHtml?.Trim() ?? string.Empty;
                existingIssue.Url = planeUrl;

                if (existingIssue.IsRemoved)
                {
                    existingIssue.IsRemoved = false;
                    existingIssue.IsCurrent = false;
                    existingIssue.Order = nextOrder++;
                }

                repoIssues.Update(existingIssue);
                continue;
            }

            var issue = new Issue
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                Url = planeUrl,
                Code = FormatCode(nextCodeNumber++),
                Title = planeIssue.Name.Trim(),
                Description = planeIssue.DescriptionHtml?.Trim() ?? string.Empty,
                Order = nextOrder++,
                IsCurrent = false,
                IsRemoved = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = participant.Id
            };

            await repoIssues.AddAsync(issue);
        }

        await repoIssues.SaveChangesAsync();

        var allIssues = (await repoIssues.GetByGameIdAsync(gameId))
            .OrderBy(issue => issue.Order)
            .ToList();

        await gameRealtimeService.NotifyIssuesImportedAsync(
            game,
            allIssues.Select(IssueMapper.ToDto));

        return allIssues.Select(IssueMapper.ToDto);
    }

    /// <summary>
    /// Eкспортуємо задачі у CSV формат
    /// </summary>
    public async Task<ExportIssuesFileDto> ExportToCsvAsync(Guid gameId, ExportIssuesRequestDto dto)
    {
        await gameAccessService.EnsureCanManageIssuesAsync(
            gameId,
            AccessControlConstants.ExportAction,
            AccessControlConstants.IssueResource);

        var issues = await repoIssues.GetByGameIdWithVotingResultsAsync(gameId);
        var builder = new StringBuilder();

        builder.AppendLine(string.Join(",",
            EscapeCsv(dto.SummaryColumnName),
            EscapeCsv(dto.KeyColumnName),
            EscapeCsv(dto.DescriptionColumnName),
            EscapeCsv(dto.LinkColumnName),
            EscapeCsv(dto.EstimateColumnName)));

        foreach (var issue in issues)
        {
            var votingResult = issue.VotingResults
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            builder.AppendLine(string.Join(",",
                EscapeCsv(issue.Title),
                EscapeCsv(issue.Code),
                EscapeCsv(issue.Description),
                EscapeCsv(issue.Url),
                EscapeCsv(votingResult?.FinalEstimate)));
        }

        return new ExportIssuesFileDto
        {
            Content = Encoding.UTF8.GetBytes(builder.ToString()),
            FileName = $"issues-{gameId}.csv"
        };
    }

    private static string FormatCode(int number) => $"PP-{number}";

    private async Task<string> GenerateIssueCodeAsync(Guid gameId)
    {
        var lastIssue = await repoIssues.GetLastCreatedIssueAsync(gameId);

        if (lastIssue == null || string.IsNullOrWhiteSpace(lastIssue.Code))
            return "PP-1";

        var parts = lastIssue.Code.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[1], out int lastNumber))
            return FormatCode(lastNumber + 1);

        return FormatCode(1);
    }

    private (string workspaceSlug, string projectId) ParsePlaneUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidUrlException("Invalid Plane URL.");

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var workspaceIndex = Array.IndexOf(segments, "projects");

        if (workspaceIndex <= 0 || workspaceIndex + 1 >= segments.Length)
            throw new InvalidUrlException("Invalid Plane URL structure.");

        return (segments[0], segments[workspaceIndex + 1]);
    }

    private static string BuildPlaneIssueUrl(string workspace, string project, string issueId)
        => $"https://app.plane.so/{workspace}/projects/{project}/issues/{issueId}";

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var escaped = value.Replace("\"", "\"\"");
        return (escaped.Contains(',') || escaped.Contains('\n') || escaped.Contains('\r'))
            ? $"\"{escaped}\""
            : escaped;
    }

    private async Task<Game> GetGameOrThrowAsync(Guid gameId)
    {
        return await gameRepository.GetByIdAsync(gameId)
               ?? throw new NotFoundException(nameof(Game), gameId);
    }
    
    private async Task<int> GetNextIssueCodeNumberAsync(Guid gameId)
    {
        var issues = await repoIssues.GetByGameIdAsync(gameId);

        var maxNumber = issues
            .Select(issue => TryParseIssueCodeNumber(issue.Code))
            .Where(number => number.HasValue)
            .Select(number => number!.Value)
            .DefaultIfEmpty(0)
            .Max();

        return maxNumber + 1;
    }

    private static int? TryParseIssueCodeNumber(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        if (!code.StartsWith("PP-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var numberPart = code["PP-".Length..];

        return int.TryParse(numberPart, out var number)
            ? number
            : null;
    }
}