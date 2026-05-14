using System.Globalization;
using PlanningPoker.BLL.Constants;
using PlanningPoker.Domain.DTOs.Room;
using PlanningPoker.Domain.DTOs.Vote;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Сервіс для керування станом кімнати планувального покеру.
/// Відповідає за отримання стану раунду, відкриття карт, скидання раунду
/// та розрахунок результатів голосування.
/// </summary>
public class RoomStateService : IRoomStateService
{
    private readonly IGameRepository _repoGames;
    private readonly IVoteRepository _repoVotes;
    private readonly IIssueRepository _repoIssues;
    private readonly IVotingHistoryRepository _repoResults;
    private readonly IGameAccessService _gameAccessService;
    private readonly ITimerService _timerService;
    private readonly IGameRealtimeService _realtimeService;

    public RoomStateService(
        IGameRepository gameRepo,
        IVoteRepository votesRepo,
        IIssueRepository repoIssues,
        IVotingHistoryRepository resultRepo,
        IGameAccessService gameAccess,
        ITimerService timerService,
        IGameRealtimeService realtimeService)
    {
        _repoGames = gameRepo;
        _repoVotes = votesRepo;
        _repoResults = resultRepo;
        _repoIssues = repoIssues;
        _gameAccessService = gameAccess;
        _timerService = timerService;
        _realtimeService = realtimeService;
    }

    /// <summary>
    /// Отримує поточний стан кімнати для гри.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Поточний стан кімнати з інформацією про голосування, учасників та активне завдання.</returns>
    public async Task<RoomStateDto> GetRoomStateAsync(Guid gameId)
    {
        var currentParticipant = await _gameAccessService.GetRequiredParticipantAsync(
            gameId,
            AccessControlConstants.UpdateAction,
            AccessControlConstants.GameResource);

        var game = await _repoGames.GetByIdAsync(gameId)
                   ?? throw new NotFoundException(nameof(Game), gameId);

        var timerDto = await _timerService.GetActiveTimerAsync(gameId);
        var isMaster = currentParticipant.Role == ParticipantRole.Master;

        var canReveal = game.RevealPolicy switch
        {
            RevealPolicy.Everyone => true,
            RevealPolicy.MasterOnly => isMaster,
            RevealPolicy.SpecificParticipants => isMaster || currentParticipant.CanRevealCards,
            _ => isMaster
        };

        var canManage = game.IssuesPolicy switch
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
            CanManageTimer = isMaster,
            AutoRevealEnabled = game.AutoRevealCards,
            Timer = timerDto,
            TotalPlayers = game.Participants.Count(participant =>
                participant.RemovedAt == null &&
                participant.Role != ParticipantRole.Spectator),
            Participants = game.Participants
                .Where(participant => participant.RemovedAt == null)
                .Select(participant => new ParticipantVoteStatusDto
                {
                    ParticipantId = participant.Id,
                    DisplayName = participant.DisplayName,
                    HasVoted = false,
                    VoteValue = null
                })
                .ToList()
        };

        if (issue is null)
        {
            return roomState;
        }

        var votes = await _repoVotes.GetVotesByIssueIdAsync(issue.Id);
        var result = await _repoResults.GetByIssueIdAsync(issue.Id);

        var isRevealed = result is not null;

        roomState.ActiveIssue = IssueMapper.ToDto(issue, result);
        roomState.IsRevealed = isRevealed;
        roomState.VotedCount = votes.Count;
        roomState.MyVote = votes.FirstOrDefault(vote =>
            vote.ParticipantId == currentParticipant.Id)?.Estimate;
        roomState.CanVote = currentParticipant.Role != ParticipantRole.Spectator
                            && !isRevealed
                            && timerDto is not { IsExpired: true };

        foreach (var participantDto in roomState.Participants)
        {
            var vote = votes.FirstOrDefault(vote =>
                vote.ParticipantId == participantDto.ParticipantId);

            participantDto.HasVoted = vote is not null;
            participantDto.VoteValue = isRevealed ? vote?.Estimate : null;
        }

        if (isRevealed && result is not null)
        {
            roomState.Result = new RoundResultDto
            {
                Average = result.Average,
                Agreement = result.Agreement,
                FinalEstimate = result.FinalEstimate
            };
        }

        return roomState;
    }

    /// <summary>
    /// Відкриває карти (завершує раунд голосування).
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Оновлений стан кімнати після відкриття карт.</returns>
    /// <exception cref="ConflictException">Виникає, якщо немає голосів.</exception>
    public async Task<RoomStateDto> RevealCardsAsync(Guid gameId)
    {
        await _gameAccessService.EnsureCanRevealCardsAsync(gameId);

        var game = await _repoGames.GetByIdAsync(gameId)
                   ?? throw new NotFoundException(nameof(Game), gameId);

        var issue = await _repoIssues.GetActiveIssueByGameIdAsync(gameId)
                    ?? throw new NotFoundException("Active issue not found for this game.");

        var existingResult = await _repoResults.GetByIssueIdAsync(issue.Id);

        if (existingResult is null)
        {
            var votes = await _repoVotes.GetVotesByIssueIdAsync(issue.Id);

            if (votes.Count == 0)
            {
                throw new ConflictException("Неможливо відкрити карти, ще ніхто не проголосував.");
            }

            var result = await CalculateSystemEstimate(votes, GetAvailableCards(game));

            var votingResult = new VotingResult
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                IssueId = issue.Id,
                FinalEstimate = result.FinalEstimate,
                Average = result.Average,
                Agreement = result.Agreement,
                CreatedAt = DateTime.UtcNow
            };
            
            await _repoResults.AddAsync(votingResult);
            await _repoResults.SaveChangesAsync();
        }

        await _timerService.StopTimerAsync(gameId);

        var roomState = await GetRoomStateAsync(gameId);
        await _realtimeService.NotifyRoundStateUpdatedAsync(game, roomState);

        return roomState;
    }

    /// <summary>
    /// Скидає поточний раунд голосування.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="issueId">Ідентифікатор issue</param>
    /// <returns>Оновлений стан кімнати після скидання раунду.</returns>
    public async Task<RoomStateDto> ResetRoundAsync(Guid gameId, Guid issueId)
    {
        await _gameAccessService.GetRequiredMasterAsync(
            gameId,
            AccessControlConstants.UpdateAction,
            AccessControlConstants.GameResource);
        
        var game = await _repoGames.GetByIdAsync(gameId)
                   ?? throw new NotFoundException(nameof(Game), gameId);

        await _timerService.StopTimerAsync(gameId);

        _ = await _repoIssues.GetByGameAndIssueAsync(gameId, issueId)
            ?? throw new NotFoundException(nameof(Issue), issueId);

        var allIssues = (await _repoIssues.GetByGameIdAsync(gameId)).ToList();

        foreach (var currentIssue in allIssues)
        {
            currentIssue.IsCurrent = currentIssue.Id == issueId;
            _repoIssues.Update(currentIssue);
        }

        var votes = await _repoVotes.GetVotesByIssueIdAsync(issueId);
        var result = await _repoResults.GetByIssueIdAsync(issueId);

        if (result is not null)
        {
            _repoResults.Delete(result);
        }

        if (votes.Count != 0)
        {
            await _repoVotes.DeleteRangeAsync(votes);
        }

        await _repoResults.SaveChangesAsync();
        var roomState = await GetRoomStateAsync(gameId);
        await _realtimeService.NotifyRoundStateUpdatedAsync(game, roomState);

        return roomState;
    }

    /// <summary>
    /// Повертає доступні карти для гри залежно від системи голосування.
    /// </summary>
    /// <param name="game">Об'єкт гри.</param>
    /// <returns>Список доступних карт.</returns>
    public List<string> GetAvailableCards(Game game)
    {
        if (game.VotingSystem != VotingSystem.Custom)
        {
            return game.VotingSystem switch
            {
                VotingSystem.Fibonacci =>
                    ["0", "1", "2", "3", "5", "8", "13", "21", "34", "55", "89", "?", "coffee"],

                VotingSystem.PowersOfTwo =>
                    ["0", "1", "2", "4", "8", "16", "32", "64", "?", "coffee"],

                VotingSystem.TShirtSizes =>
                    ["XS", "S", "M", "L", "XL", "XXL", "?", "coffee"],

                _ => ["?", "coffee"]
            };
        }

        if (string.IsNullOrWhiteSpace(game.CustomValues))
        {
            return ["?", "coffee"];
        }

        var cards = game.CustomValues
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!cards.Contains("?", StringComparer.OrdinalIgnoreCase))
        {
            cards.Add("?");
        }

        if (!cards.Contains("coffee", StringComparer.OrdinalIgnoreCase))
        {
            cards.Add("coffee");
        }

        return cards;
    }

    public async Task<RoundResultDto> GetFinalEstimateAsync(Guid gameId, Guid issueId)
    {
        await _gameAccessService.GetRequiredParticipantAsync(
            gameId,
            AccessControlConstants.UpdateAction,
            AccessControlConstants.GameResource);

        var result = await _repoResults.GetByIssueIdAsync(issueId);

        if (result is null)
        {
            throw new ConflictException("Результати ще недоступні.");
        }

        return new RoundResultDto
        {
            Average = result.Average,
            Agreement = result.Agreement,
            FinalEstimate = result.FinalEstimate
        };
    }

    /// <summary>
    /// Розраховує фінальну оцінку, середнє значення та agreement.
    /// Agreement = кількість голосів за найчастішу оцінку / кількість валідних голосів * 100.
    /// FinalEstimate:
    /// - якщо є одна найчастіша оцінка, береться вона;
    /// - якщо є нічия, береться компромісна оцінка за порядком карт.
    /// </summary>
    private Task<(string FinalEstimate, double? Average, double? Agreement)> CalculateSystemEstimate(
        List<Vote> votes,
        List<string> availableCards)
    {
        var validVotes = votes
            .Where(vote => !string.IsNullOrWhiteSpace(vote.Estimate))
            .Where(vote => !IsSpecialCard(vote.Estimate))
            .ToList();

        if (validVotes.Count == 0)
        {
            return Task.FromResult<(string FinalEstimate, double? Average, double? Agreement)>(
                ("?", null, null));
        }

        var orderedCards = availableCards
            .Where(card => !string.IsNullOrWhiteSpace(card))
            .Where(card => !IsSpecialCard(card))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var agreement = CalculateModeAgreement(validVotes);
        var average = TryCalculateNumericAverage(validVotes);

        var voteGroups = validVotes
            .GroupBy(vote => vote.Estimate, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Estimate = ResolveCardLabel(group.Key, orderedCards),
                Count = group.Count()
            })
            .OrderByDescending(group => group.Count)
            .ToList();

        var maxCount = voteGroups.First().Count;

        var modeCandidates = voteGroups
            .Where(group => group.Count == maxCount)
            .Select(group => group.Estimate)
            .ToList();

        if (modeCandidates.Count == 1)
        {
            return Task.FromResult<(string FinalEstimate, double? Average, double? Agreement)>(
                (modeCandidates[0], average, agreement));
        }

        var isNumericDeck = orderedCards.Count > 0 &&
                            orderedCards.All(card =>
                                double.TryParse(
                                    card,
                                    NumberStyles.Float,
                                    CultureInfo.InvariantCulture,
                                    out _));

        if (isNumericDeck)
        {
            var result = CalculateNumericConsensusEstimate(validVotes, orderedCards, agreement);
            return Task.FromResult(result);
        }

        var orderedResult = CalculateOrderedConsensusEstimate(validVotes, orderedCards, agreement);
        return Task.FromResult(orderedResult);
    }

    private static bool IsSpecialCard(string value)
    {
        return string.Equals(value, "coffee", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "?", StringComparison.OrdinalIgnoreCase);
    }

    private static double CalculateModeAgreement(List<Vote> validVotes)
    {
        var maxVotesForOneEstimate = validVotes
            .GroupBy(vote => vote.Estimate, StringComparer.OrdinalIgnoreCase)
            .Max(group => group.Count());

        return Math.Round((double)maxVotesForOneEstimate / validVotes.Count * 100);
    }

    private static double? TryCalculateNumericAverage(List<Vote> validVotes)
    {
        var numericVotes = validVotes
            .Select(vote =>
                double.TryParse(
                    vote.Estimate,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value)
                    ? value
                    : (double?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        return numericVotes.Count == 0
            ? null
            : Math.Round(numericVotes.Average(), 1);
    }

    private static (string FinalEstimate, double? Average, double? Agreement) CalculateNumericConsensusEstimate(
        List<Vote> validVotes,
        List<string> orderedCards,
        double agreement)
    {
        var numericVotes = validVotes
            .Select(vote =>
                double.TryParse(
                    vote.Estimate,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value)
                    ? value
                    : (double?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        if (numericVotes.Count == 0)
        {
            return CalculateOrderedConsensusEstimate(validVotes, orderedCards, agreement);
        }

        var average = Math.Round(numericVotes.Average(), 1);

        var numericCards = orderedCards
            .Select(card =>
            {
                var parsed = double.TryParse(
                    card,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value);

                return parsed ? new NumericCard(card, value) : null;
            })
            .Where(card => card is not null)
            .Select(card => card!)
            .ToList();

        if (numericCards.Count == 0)
        {
            return (numericVotes.Last().ToString(CultureInfo.InvariantCulture), average, agreement);
        }

        var finalEstimate = numericCards
            .OrderBy(card => Math.Abs(card.Value - average))
            .ThenByDescending(card => card.Value)
            .First()
            .Card;

        return (finalEstimate, average, agreement);
    }

    private static (string FinalEstimate, double? Average, double? Agreement) CalculateOrderedConsensusEstimate(
        List<Vote> validVotes,
        List<string> orderedCards,
        double agreement)
    {
        if (orderedCards.Count == 0)
        {
            var fallbackEstimate = validVotes
                .GroupBy(vote => vote.Estimate, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .First()
                .Key;

            return (fallbackEstimate, null, agreement);
        }

        var cardIndexes = orderedCards
            .Select((card, index) => new
            {
                Card = card,
                Index = index
            })
            .ToDictionary(
                item => item.Card,
                item => item.Index,
                StringComparer.OrdinalIgnoreCase);

        var voteIndexes = validVotes
            .Where(vote => cardIndexes.ContainsKey(vote.Estimate))
            .Select(vote => cardIndexes[vote.Estimate])
            .ToList();

        if (voteIndexes.Count == 0)
        {
            var fallbackEstimate = validVotes
                .GroupBy(vote => vote.Estimate, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .First()
                .Key;

            return (fallbackEstimate, null, agreement);
        }

        var averageIndex = voteIndexes.Average();

        var finalEstimate = orderedCards
            .Select((card, index) => new
            {
                Card = card,
                Index = index,
                Distance = Math.Abs(index - averageIndex)
            })
            .OrderBy(item => item.Distance)
            .ThenByDescending(item => item.Index)
            .First()
            .Card;

        return (finalEstimate, null, agreement);
    }

    private static string ResolveCardLabel(string value, List<string> orderedCards)
    {
        return orderedCards.FirstOrDefault(card =>
            string.Equals(card, value, StringComparison.OrdinalIgnoreCase)) ?? value;
    }

    private sealed record NumericCard(string Card, double Value);
}
