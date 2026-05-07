namespace PlanningPoker.Domain.DTOs.VotingHistory;

/// <summary>
/// Один запис історії абоо завершеного результату
/// </summary>
public class VotingHistoryItemDto
{
    public Guid Id {get; set; }
    public Guid? IssueId {get; set; }
    public string IssueName {get; set; }
    public string? Result {get; set; }
    public double? Average { get; set;}
    public string? MostVotedCard { get; set;}
    public byte AgreementPercent { get; set; }
    public string AgreementLevel { get; set; } = string.Empty;
    public TimeSpan? Duration { get; set;}
    public DateTime CompletedAt { get; set;}
    public int TotalPlayers {get; set; }
    public int VotedCount { get; set; }
    public List<VoteResultDto> PlayerResults {get; set; } = new();
}

