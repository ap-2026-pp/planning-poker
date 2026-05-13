using PlanningPoker.Domain.DTOs.Room;
using PlanningPoker.Domain.DTOs.Vote;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.BLL.Constants;
using System.Globalization;

namespace PlanningPoker.BLL.Services;

public class RoomStateService : IRoomStateService
{
    private readonly IGameRepository _repoGames;
    private readonly IVoteRepository _repoVotes;
    private readonly IIssueRepository _repoIssues;
    private readonly IVotingHistoryRepository _repoResults;
    private readonly IGameAccessService _gameAccessService;
    private readonly ITimerService _timerService; 

    public RoomStateService(
        IGameRepository gameRepo,
        IVoteRepository votesRepo,
        IIssueRepository repoIssues,
        IVotingHistoryRepository resultRepo,
        IGameAccessService gameAccess,
        ITimerService timerService) 
    {
        _repoGames = gameRepo;
        _repoVotes = votesRepo;
        _repoResults = resultRepo;
        _repoIssues = repoIssues;
        _gameAccessService = gameAccess;
        _timerService = timerService;
    }

    public async Task<RoomStateDto> GetRoomStateAsync(Guid gameId)
    {
        var currentParticipant = await _gameAccessService.GetRequiredParticipantAsync(gameId, AccessControlConstants.UpdateAction, AccessControlConstants.GameResource);
        var game = await _repoGames.GetByIdAsync(gameId) ?? throw new NotFoundException("Game", gameId);
        
        var timerDto = await _timerService.GetActiveTimerAsync(gameId);
        bool isMaster = currentParticipant.Role == ParticipantRole.Master;

        bool timerIsRunning = timerDto != null && !timerDto.IsExpired;

        bool canReveal = !timerIsRunning && (game.RevealPolicy switch
        {
            RevealPolicy.Everyone => true,
            RevealPolicy.MasterOnly => isMaster,
            RevealPolicy.SpecificParticipants => isMaster || currentParticipant.CanRevealCards,
            _ => isMaster
        });

        bool canManage = game.IssuesPolicy switch
        {
            IssuesPolicy.Everyone => true,
            IssuesPolicy.MasterOnly => isMaster,
            IssuesPolicy.SpecificParticipants => isMaster || currentParticipant.CanManageIssues,
            _ => isMaster
        };

        var issue = await _repoIssues.GetActiveIssueByGameIdAsync(gameId);

        var roomState = new RoomStateDto
        {
            GameId = game.Id,
            VotingSystem = game.VotingSystem.ToString(),
            AvailableCards = GetAvailableCards(game),
            CanReveal = canReveal,
            CanManage = canManage,
            Timer = timerDto, 
            TotalPlayers = game.Participants.Count(p => p.RemovedAt == null && p.Role != ParticipantRole.Spectator),
            Participants = game.Participants
                .Where(p => p.RemovedAt == null)
                .Select(p => new ParticipantVoteStatusDto
                {
                    ParticipantId = p.Id,
                    DisplayName = p.DisplayName,
                    HasVoted = false
                }).ToList()
        };

        if (issue == null) return roomState;

        var votes = await _repoVotes.GetVotesByIssueIdAsync(issue.Id);
        var result = await _repoResults.GetByIssueIdAsync(issue.Id);

        bool isRevealed = result != null || (timerDto != null && timerDto.IsExpired);

        roomState.ActiveIssue = IssueMapper.ToDto(issue);
        roomState.IsRevealed = isRevealed;
        roomState.VotedCount = votes.Count;
        roomState.MyVote = votes.FirstOrDefault(v => v.ParticipantId == currentParticipant.Id)?.Estimate;
        roomState.CanVote = currentParticipant.Role != ParticipantRole.Spectator && !isRevealed;

        foreach (var pDto in roomState.Participants)
        {
            var vote = votes.FirstOrDefault(v => v.ParticipantId == pDto.ParticipantId);
            pDto.HasVoted = vote != null;
            pDto.VoteValue = isRevealed ? vote?.Estimate : null;
        }

        if (isRevealed && result != null)
        {
            roomState.Result = new RoundResultDto
            {
                Average = result!.Average,
                Agreement = result.Agreement,
                FinalEstimate = result.FinalEstimate
            };
        }

        return roomState;
    }

    public async Task<RoomStateDto> RevealCardsAsync(Guid gameId)
    {
        var timer = await _timerService.GetActiveTimerAsync(gameId);
        if (timer != null && !timer.IsExpired)
        {
            throw new ConflictException("Неможливо відкрити карти: триває таймер обговорення.");
        }

        await _gameAccessService.EnsureCanRevealCardsAsync(gameId);
        var issue = await _repoIssues.GetActiveIssueByGameIdAsync(gameId)
                ?? throw new NotFoundException("Active issue not found for this game.");

        var issueId = issue.Id;
        var existingResult = await _repoResults.GetByIssueIdAsync(issueId);
        if (existingResult != null)
        {
            return await GetRoomStateAsync(gameId);
        }

        var votes = await _repoVotes.GetVotesByIssueIdAsync(issueId);
        if (!votes.Any())
        {
            throw new ConflictException("Неможливо відкрити карти, ще ніхто не проголосував.");
        }

        var result = await CalculateSystemEstimate(votes);

        var votingResult = new VotingResult
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            IssueId = issueId,
            FinalEstimate = result.FinalEstimate,
            Average = result.Average,
            Agreement = result.Agreement,
            CreatedAt = DateTime.UtcNow
        };

        await _repoResults.AddAsync(votingResult);
        await _repoResults.SaveChangesAsync();

        return await GetRoomStateAsync(gameId);
    }

    public async Task<RoomStateDto> ResetRoundAsync(Guid gameId)
    {
        await _gameAccessService.GetRequiredMasterAsync(gameId, AccessControlConstants.UpdateAction, AccessControlConstants.GameResource);
        
        await _timerService.StopTimerAsync(gameId);

        var issue = await _repoIssues.GetActiveIssueByGameIdAsync(gameId)
                ?? throw new NotFoundException("Active issue not found.");

        var issueId = issue.Id;
        var votes = await _repoVotes.GetVotesByIssueIdAsync(issueId);
        var result = await _repoResults.GetByIssueIdAsync(issueId);

        if (result != null) _repoResults.Delete(result);
        if (votes.Any()) await _repoVotes.DeleteRangeAsync(votes);

        await _repoResults.SaveChangesAsync();
        await _repoVotes.SaveChangesAsync();

        return await GetRoomStateAsync(gameId);
    }

    public List<string> GetAvailableCards(Game game)
    {
        if (game.VotingSystem == VotingSystem.Custom)
        {
            if (string.IsNullOrWhiteSpace(game.CustomValues)) return ["?", "coffee"];
            var cards = game.CustomValues.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList();
            if (!cards.Contains("?")) cards.Add("?");
            if (!cards.Contains("coffee")) cards.Add("coffee");
            return cards;
        }
        return game.VotingSystem switch
        {
            VotingSystem.Fibonacci => ["0", "1", "2", "3", "5", "8", "13", "21", "34", "55", "89", "?", "coffee"],
            VotingSystem.PowersOfTwo => ["0", "1", "2", "4", "8", "16", "32", "64", "?", "coffee"],
            VotingSystem.TShirtSizes => ["XS", "S", "M", "L", "XL", "XXL", "?", "coffee"],
            _ => ["?", "coffee"]
        };
    }

    public async Task<RoundResultDto> GetFinalEstimateAsync(Guid gameId, Guid issueId)
    {
        await _gameAccessService.GetRequiredParticipantAsync(gameId, AccessControlConstants.UpdateAction, AccessControlConstants.GameResource);
        var result = await _repoResults.GetByIssueIdAsync(issueId);
        if (result == null) throw new ConflictException("Результати ще недоступні.");
        return new RoundResultDto { Average = result.Average, Agreement = result.Agreement, FinalEstimate = result.FinalEstimate };
    }

    public async Task<(string FinalEstimate, double? Average, double? Agreement)> CalculateSystemEstimate(List<Vote> votes)
    {
        var numericVotes = votes.Select(vote => double.TryParse(vote.Estimate, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : (double?)null)
            .Where(v => v.HasValue).Select(v => v!.Value).ToList();
        if (!numericVotes.Any()) return ("?", null, null);
        var average = Math.Round(numericVotes.Average(), 1);
        var finalEstimate = Math.Round(average).ToString(CultureInfo.InvariantCulture);
        var sameVotesCount = numericVotes.Count(v => Math.Abs(v - average) < 0.1);
        var agreement = Math.Round((double)sameVotesCount / numericVotes.Count * 100);
        return (finalEstimate, average, agreement);
    }
}