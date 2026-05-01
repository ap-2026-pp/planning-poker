using System.Text;
using PlanningPoker.Domain.DTOs.VotingHistory;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;

namespace PlanningPoker.BLL.Services;

internal class VotingHistoryService : IVotingHistoryService
{
    private readonly IVotingHistoryRepository _repoVotingHistory;

    public VotingHistoryService(IVotingHistoryRepository repoVotingHistory)
    {
        _repoVotingHistory = repoVotingHistory;
    }

    public async Task<VotingHistoryListDto> GetHistoryAsync(
        Guid gameId,
        VotingHistoryQueryDto query)
    {
        var results = await _repoVotingHistory.GetHistoryRawAsync(gameId);

        var items = results
            .Select(VotingHistoryMapper.ToItemDto)
            .ToList();

        items = ApplySorting(items, query.SortBy, query.SortDirection);

        var totalCount = items.Count;

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var pagedItems = items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new VotingHistoryListDto
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Items = pagedItems
        };
    }

    public async Task<VotingHistoryDetailsDto> GetHistoryDetailsAsync(
        Guid gameId,
        Guid entryId)
    {
        var result = await _repoVotingHistory.GetHistoryDetailsRawAsync(gameId, entryId);

        if (result is null)
        {
            throw new KeyNotFoundException("Voting history entry was not found.");
        }

        return VotingHistoryMapper.ToDetailsDto(result);
    }

    public async Task<byte[]> ExportHistoryCsvAsync(
        Guid gameId,
        ExportVotingHistoryDto dto)
    {
        var history = await GetHistoryAsync(gameId, new VotingHistoryQueryDto
        {
            Page = 1,
            PageSize = int.MaxValue
        });

        var sb = new StringBuilder();

        var headers = new List<string>();

        if (dto.IncludeIssue)
            headers.Add("Issue");

        if (dto.IncludeResult)
            headers.Add("Result");

        if (dto.IncludeAverage)
            headers.Add("Average");

        if (dto.IncludeMostVotedCard)
            headers.Add("Most voted card");

        if (dto.IncludeAgreement)
            headers.Add("Agreement");

        if (dto.IncludeDuration)
            headers.Add("Duration");

        if (dto.IncludePlayersVoted)
            headers.Add("Players voted");

        if (dto.IncludePlayersTotal)
            headers.Add("Players total");

        if (dto.IncludeTime)
            headers.Add("Time");

        if (dto.IncludeResults)
            headers.Add("Player results");

        sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

        foreach (var item in history.Items)
        {
            var row = new List<string>();

            if (dto.IncludeIssue)
                row.Add(item.IssueName);

            if (dto.IncludeResult)
                row.Add(item.Result ?? string.Empty);

            if (dto.IncludeAverage)
                row.Add(item.Average?.ToString("0.##") ?? string.Empty);

            if (dto.IncludeMostVotedCard)
                row.Add(item.MostVotedCard ?? string.Empty);

            if (dto.IncludeAgreement)
                row.Add($"{item.AgreementPercent}%");

            if (dto.IncludeDuration)
                row.Add(item.Duration?.ToString() ?? string.Empty);

            if (dto.IncludePlayersVoted)
                row.Add(item.VotedCount.ToString());

            if (dto.IncludePlayersTotal)
                row.Add(item.TotalPlayers.ToString());

            if (dto.IncludeTime)
                row.Add(item.CompletedAt.ToString("yyyy-MM-dd HH:mm"));

            if (dto.IncludeResults)
            {
                row.Add(string.Join("; ",
                    item.PlayerResults.Select(player =>
                        $"{player.DisplayName} ({player.VoteValue})")));
            }

            sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static List<VotingHistoryItemDto> ApplySorting(
        List<VotingHistoryItemDto> items,
        string? sortBy,
        string? sortDirection)
    {
        var isDesc = string.Equals(
            sortDirection,
            "desc",
            StringComparison.OrdinalIgnoreCase);

        return sortBy?.ToLower() switch
        {
            "issue" => isDesc
                ? items.OrderByDescending(item => item.IssueName).ToList()
                : items.OrderBy(item => item.IssueName).ToList(),

            "result" => isDesc
                ? items.OrderByDescending(item => item.Result).ToList()
                : items.OrderBy(item => item.Result).ToList(),

            "average" => isDesc
                ? items.OrderByDescending(item => item.Average).ToList()
                : items.OrderBy(item => item.Average).ToList(),

            "mostvotedcard" => isDesc
                ? items.OrderByDescending(item => item.MostVotedCard).ToList()
                : items.OrderBy(item => item.MostVotedCard).ToList(),

            "agreement" => isDesc
                ? items.OrderByDescending(item => item.AgreementPercent).ToList()
                : items.OrderBy(item => item.AgreementPercent).ToList(),

            "duration" => isDesc
                ? items.OrderByDescending(item => item.Duration).ToList()
                : items.OrderBy(item => item.Duration).ToList(),

            "date" or "time" or _ => isDesc
                ? items.OrderByDescending(item => item.CompletedAt).ToList()
                : items.OrderBy(item => item.CompletedAt).ToList()
        };
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}