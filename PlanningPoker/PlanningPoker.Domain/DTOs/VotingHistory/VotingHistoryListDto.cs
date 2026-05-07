namespace PlanningPoker.Domain.DTOs.VotingHistory;

/// <summary>
/// DTO для відповіді зі списком voting history
/// </summary>
public class VotingHistoryListDto
{
    public int TotalCount { get; set;}
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<VotingHistoryItemDto> Items { get; set; } = new();

}
