namespace PlanningPoker.Domain.DTOs.VotingHistory;

/// <summary>
/// Dto для query-параметрів voting history.
/// Приймає фільтрацію та пагацію 
/// </summary>
public class VotingHistoryQueryDto
{
    public string? SortBy { get; set;} = "date";
    public string? SortDirection { get; set;} = "desc";
    public int Page { get; set;} = 1;
    public int PageSize { get; set;} = 10;
}