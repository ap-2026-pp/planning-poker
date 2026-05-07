using PlanningPoker.Domain.DTOs.VotingHistory;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Mappers;

public static class VotingHistoryMapper
{
    public static VotingHistoryItemDto ToItemDto(VotingResult result)
    {
        var votes = result.Issue.Votes.ToList();

        var playerResults = votes
            .Select(ToVoteResultDto)
            .ToList();

        var agreementPercent = CalculateAgreementPercent(
            playerResults.Select(vote => vote.VoteValue).ToList(),
            result.FinalEstimate);

        return new VotingHistoryItemDto
        {
            Id = result.Id,
            IssueId = result.IssueId,
            IssueName = result.Issue.Title,
            Result = result.FinalEstimate,
            Average = result.Average,
            MostVotedCard = GetMostVoted(playerResults),
            AgreementPercent = agreementPercent,
            AgreementLevel = GetAgreementLevel(agreementPercent),
            Duration = CalculateDuration(votes),
            CompletedAt = result.CreatedAt,
            VotedCount = votes.Count,
            TotalPlayers = votes.Select(vote => vote.ParticipantId).Distinct().Count(),
            PlayerResults = playerResults
        };
    }

    public static VotingHistoryDetailsDto ToDetailsDto(VotingResult result)
    {
        var item = ToItemDto(result);

        return new VotingHistoryDetailsDto
        {
            Id = item.Id,
            GameId = result.GameId,
            IssueId = item.IssueId,
            IssueName = item.IssueName,
            Result = item.Result,
            Average = item.Average,
            MostVotedCard = item.MostVotedCard,
            AgreementPercent = item.AgreementPercent,
            AgreementLevel = item.AgreementLevel,
            Duration = item.Duration,
            CompletedAt = item.CompletedAt,
            VotedCount = item.VotedCount,
            TotalPlayers = item.TotalPlayers,
            PlayerResults = item.PlayerResults
        };
    }

    public static VoteResultDto ToVoteResultDto(Vote vote)
    {
        return new VoteResultDto
        {
            ParticipantId = vote.ParticipantId,
            DisplayName = vote.Participant.DisplayName,
            VoteValue = vote.FinalEstimate
        };
    }

    private static byte CalculateAgreementPercent(
        List<string> votes,
        string finalResult)
    {
        if (votes.Count == 0 || string.IsNullOrWhiteSpace(finalResult))
        {
            return 0;
        }

        var sameVotesCount = votes.Count(vote => vote == finalResult);

        return (byte)Math.Round((double)sameVotesCount / votes.Count * 100);
    }

    private static string GetAgreementLevel(byte percent)
    {
        return percent switch
        {
            100 => "Full",
            >= 70 => "High",
            >= 40 => "Medium",
            > 0 => "Low",
            _ => "None"
        };
    }

    private static string? GetMostVoted(List<VoteResultDto> votes)
    {
        return votes
            .Where(vote => !string.IsNullOrWhiteSpace(vote.VoteValue))
            .GroupBy(vote => vote.VoteValue)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault()
            ?.Key;
    }

    private static TimeSpan? CalculateDuration(List<Vote> votes)
    {
        if (votes.Count == 0)
        {
            return null;
        }

        var start = votes.Min(vote => vote.CreatedAt);
        var end = votes.Max(vote => vote.UpdatedAt ?? vote.CreatedAt);

        return end - start;
    }
}