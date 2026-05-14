using PlanningPoker.Domain.DTOs.Vote;
namespace PlanningPoker.Domain.DTOs.VotingHistory;

/// <summary>
/// Деталі одного завершеного раунду голосування.
/// Повертається при натисканні на кнопку перегляду деталей
/// </summary>
public class VotingHistoryDetailsDto
{
    public Guid Id { get; set; }

    public Guid GameId { get; set; }

    public Guid? IssueId { get; set; }

    public string IssueName { get; set; } = "-";

    public string? Result { get; set; }

    public double? Average { get; set; }

    public string? MostVotedCard { get; set; }

    public byte AgreementPercent { get; set; }

    public string AgreementLevel { get; set; } = string.Empty;

    public TimeSpan? Duration { get; set; }

    public DateTime CompletedAt { get; set; }

    public int VotedCount { get; set; }

    public int TotalPlayers { get; set; }

    public List<VoteResultDto> VotingResults { get; set; } = new();
}