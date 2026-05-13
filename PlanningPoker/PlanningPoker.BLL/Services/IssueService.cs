using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.BLL.DTOs.Issue.Export;
using PlanningPoker.BLL.DTOs.Plane;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;
using System.Data;
using System.Text;
using PlanningPoker.BLL.Constants;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Сервіс для управління задачами (Issues) у межах ігрової сесії.
/// </summary>
public class IssueService(
    IIssueRepository repoIssues,
    IPlaneService planeService,
    IGameAccessService gameAccessService) : IIssueService
{

    public async Task<IEnumerable<IssueDto>> GetIssuesByGameAsync(Guid gameId)
    {
        await gameAccessService.GetRequiredParticipantAsync(
            gameId,
            AccessControlConstants.ViewAction,
            AccessControlConstants.IssuesResource);
        var issues = await repoIssues.GetByGameIdAsync(gameId);

        return issues.Select(issue => IssueMapper.ToDto(issue));
    }

    public async Task<IssueDetailsDto> GetIssueByIdAsync(Guid gameId, Guid issueId)
    {
        await gameAccessService.GetRequiredParticipantAsync(
            gameId,
            AccessControlConstants.ViewAction,
            AccessControlConstants.IssueResource);

        var issue = await repoIssues.GetByGameAndIssueAsync(gameId, issueId)
                    ?? throw new NotFoundException(nameof(Issue), issueId);

        return IssueMapper.ToDetailsDto(issue, issue.VotingResults?.FirstOrDefault());
    }

    /// <summary>
    /// Створює нову задачу в грі, якщо поточному учаснику дозволено керувати issues згідно з політикою гри.
    /// </summary>
    public async Task<IssueDto> CreateIssueAsync(Guid gameId, CreateIssueDto dto)
    {
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

        return IssueMapper.ToDto(issue);
    }

    public async Task<IssueDto> UpdateIssueAsync(Guid gameId, Guid issueId, UpdateIssueDto dto)
    {
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

        return IssueMapper.ToDto(issue);
    }

    public async Task DeleteIssueAsync(Guid gameId, Guid issueId)
    {
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
    }

    public async Task DeleteAllIssuesAsync(Guid gameId)
    {
        await gameAccessService.EnsureCanManageIssuesAsync(
            gameId,
            AccessControlConstants.DeleteAction,
            AccessControlConstants.IssueResource);

        var issues = await repoIssues.GetByGameIdAsync(gameId);

        foreach (var issue in issues)
        {
            issue.IsRemoved = true;
            issue.IsCurrent = false;
        }

        await repoIssues.SaveChangesAsync();
    }

    public async Task ReorderIssuesAsync(Guid gameId, ReorderIssueDto dto)
    {
        await gameAccessService.EnsureCanManageIssuesAsync(
            gameId,
            AccessControlConstants.ReorderAction,
            AccessControlConstants.IssueResource);

        var issues = await repoIssues.GetIssuesByIdsAsync(gameId, dto.IssuesIds);
        var issuesList = issues.ToList();

        if (issuesList.Count != dto.IssuesIds.Count)
            throw new InvalidOperationException("Invalid issues list.");

        for (var i = 0; i < dto.IssuesIds.Count; i++)
        {
            var issue = issuesList.First(x => x.Id == dto.IssuesIds[i]);
            issue.Order = i + 1;
        }

        await repoIssues.SaveChangesAsync();
    }

    public async Task<IssueDto> SetIssueActiveAsync(Guid gameId, Guid issueId)
    {
        await gameAccessService.GetRequiredMasterAsync(
            gameId,
            AccessControlConstants.SetAction,
            AccessControlConstants.IssueResource);
        var issue = await repoIssues.GetByGameAndIssueAsync(gameId, issueId)
                    ?? throw new NotFoundException(nameof(Issue), issueId);

        await repoIssues.ClearCurrentIssueAsync(gameId);

        issue.IsCurrent = true;

        repoIssues.Update(issue);
        await repoIssues.SaveChangesAsync();

        return IssueMapper.ToDto(issue);
    }

    public async Task<Issue> GetAndValidateActiveIssueAsync(Guid gameId, Guid issueId)
    {
        var issue = await repoIssues.GetByIdAsync(issueId)
            ?? throw new NotFoundException("Issue", issueId);

        if (issue.GameId != gameId)
        {
            throw new NotFoundException($"Issue {issueId} не знайдено в межах поточної гри.");
        }

        if (!issue.IsCurrent)
        {
            throw new InvalidOperationException("Голосування можливе тільки для активного питання (IsCurrent).");
        }

        if (issue.IsRemoved)
        {
            throw new NotFoundException("Issue", issueId);
        }

        return issue;
    }

    public async Task<IEnumerable<IssueDto>> ImportIssueByPlaneAsync(Guid gameId, ImportPlaneIssuesDto dto)
    {
        var participant = await gameAccessService.EnsureCanManageIssuesAsync(
            gameId,
            AccessControlConstants.ImportAction,
            AccessControlConstants.IssueResource);

        var (workspaceSlug, projectId) = ParsePlaneUrl(dto.ProjectUrl);

        var planeIssues = await planeService.GetIssuesAsync(gameId, workspaceSlug, projectId, dto.ApiKey);
        var nextOrder = await repoIssues.GetNextOrderAsync(gameId);

        foreach (var planeIssue in planeIssues)
        {
            var planeUrl = BuildPlaneIssueUrl(workspaceSlug, projectId, planeIssue.Id);

            if (await repoIssues.ExistsByUrlAsync(gameId, planeUrl))
                continue;

            var lastIssue = await repoIssues.GetLastCreatedIssueAsync(gameId);
            var nextNumber = lastIssue == null
                ? 1
                : int.Parse(lastIssue.Code.Replace("PP-", "")) + 1;

            var issue = new Issue
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                Url = planeUrl,
                Code = FormatCode(nextNumber),
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

        var allIssues = await repoIssues.GetByGameIdAsync(gameId);
        return allIssues.Select(i => IssueMapper.ToDto(i, i.VotingResults?.FirstOrDefault()));
    }

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
}