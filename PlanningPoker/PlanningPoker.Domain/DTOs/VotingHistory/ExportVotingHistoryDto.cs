namespace PlanningPoker.Domain.DTOs.VotingHistory;

/// <summary>
/// Dto для вибору колонок під час експоорту CSV
/// </summary>
public class ExportVotingHistoryDto
{
    public bool IncludeIssue { get; set;}
    public bool IncludeResult { get; set;}
    public bool IncludeAverage { get; set;}
    public bool IncludeMostVotedCard { get; set;}
    public bool IncludeAgreement { get; set;}
    public bool IncludeDuration { get; set;}
    public bool IncludePlayersVoted { get; set;}
    public bool IncludePlayersTotal { get; set;}
    public bool IncludeTime { get; set;}
    public bool IncludeResults { get; set;}
}