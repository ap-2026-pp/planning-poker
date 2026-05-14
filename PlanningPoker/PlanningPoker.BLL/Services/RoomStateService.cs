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
                   ?? throw new NotFoundException("Game", gameId);

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
    /// <exception cref="ConflictException">Виникає, якщо таймер активний або немає голосів.</exception>
    public async Task<RoomStateDto> RevealCardsAsync(Guid gameId)
    {
        var timer = await _timerService.GetActiveTimerAsync(gameId);
        if (timer != null && !timer.IsExpired)
        {
            throw new ConflictException("Неможливо відкрити карти: триває таймер обговорення.");
        }

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

            var result = await CalculateSystemEstimate(votes);

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
        if (game.VotingSystem == VotingSystem.Custom)
        {
            if (string.IsNullOrWhiteSpace(game.CustomValues)) return ["?", "coffee"];

            var cards = game.CustomValues
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .ToList();

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

    /// <summary>
    /// Отримує фінальну оцінку для завершеного раунду.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="issueId">Ідентифікатор задачі.</param>
    /// <returns>Фінальний результат голосування.</returns>
    /// <exception cref="ConflictException">Виникає, якщо результати ще не доступні.</exception>
    public async Task<RoundResultDto> GetFinalEstimateAsync(Guid gameId, Guid issueId)
    {
        await _gameAccessService.GetRequiredParticipantAsync(
            gameId,
            AccessControlConstants.UpdateAction,
            AccessControlConstants.GameResource);

        var result = await _repoResults.GetByIssueIdAsync(issueId);
        if (result == null)
            throw new ConflictException("Результати ще недоступні.");

        return new RoundResultDto
        {
            Average = result.Average,
            Agreement = result.Agreement,
            FinalEstimate = result.FinalEstimate
        };
    }

    /// <summary>
    /// Розраховує системну оцінку на основі голосів.
    /// </summary>
    /// <param name="votes">Список голосів учасників.</param>
    /// <returns>Кортеж із фінальною оцінкою, середнім значенням та рівнем узгодженості.</returns>
    public async Task<(string FinalEstimate, double? Average, double? Agreement)> CalculateSystemEstimate(
        List<Vote> votes)
    {
        var numericVotes = votes
            .Select(vote =>
                double.TryParse(vote.Estimate, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    ? value
                    : (double?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        if (!numericVotes.Any())
            return ("?", null, null);

        var average = Math.Round(numericVotes.Average(), 1);
        var finalEstimate = Math.Round(average).ToString(CultureInfo.InvariantCulture);
        var sameVotesCount = numericVotes.Count(v => Math.Abs(v - average) < 0.1);
        var agreement = Math.Round((double)sameVotesCount / numericVotes.Count * 100);

        return (finalEstimate, average, agreement);
    }
}