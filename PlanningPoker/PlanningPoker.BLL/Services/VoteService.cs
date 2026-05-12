using System.Globalization;
using System.ComponentModel.DataAnnotations;
using PlanningPoker.Domain.DTOs.Vote;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

internal class VoteService : IVoteService
{
    private readonly IVoteRepository _repoVotes;
    private readonly IVotingHistoryRepository _repoResults;
    private readonly IIssueService _issueService;
    private readonly IRoomStateService _roomService;
    private readonly IGameAccessService _gameAccessService;
    private readonly IGameRepository _gameRepository;
    public VoteService(
        IVoteRepository repoVotes,
        IVotingHistoryRepository repoResults,
        IRoomStateService roomService,
        IIssueService issueService,
        IGameAccessService gameAccessService,
        IGameRepository gameRepository)
    {
        _repoVotes = repoVotes;
        _repoResults = repoResults;
        _gameAccessService = gameAccessService;
        _roomService = roomService;
        _issueService = issueService;
        _gameRepository = gameRepository;
    }

    public async Task<VoteDto> CreateVoteByGameIdAsync(Guid gameId, Guid issueId, CreateVoteDto dto)
    {
        var participant = await _gameAccessService.EnsureCanVoteAsync(gameId);

        var game = await _gameRepository.GetByIdAsync(gameId)
               ?? throw new NotFoundException(nameof(Game), gameId);

        await _issueService.GetAndValidateActiveIssueAsync(gameId, issueId);

        var allowedCards = _roomService.GetAvailableCards(game);

        if (!allowedCards.Contains(dto.Estimate))
        {
            throw new ValidationException($"Значення '{dto.Estimate}' недопустиме для цієї системи голосування.");
        }

        var existingResult = await _repoResults.GetByIssueIdAsync(issueId);
        if (existingResult != null)
            throw new ConflictException("Неможливо змінити голос: карти вже відкриті.");

        var vote = await _repoVotes.GetVoteAsync(issueId, participant.Id);

        if (vote != null)
        {
            vote.Estimate = dto.Estimate;
            vote.UpdatedAt = DateTime.UtcNow;
            _repoVotes.Update(vote);
        }
        else
        {
            vote = new Vote
            {
                Id = Guid.NewGuid(),
                IssueId = issueId,
                ParticipantId = participant.Id,
                Estimate = dto.Estimate,
                CreatedAt = DateTime.UtcNow
            };

            await _repoVotes.AddAsync(vote);
        }

        await _repoVotes.SaveChangesAsync();

        return VoteMapper.ToDto(vote);
    }

    public async Task DeleteVoteAsync(Guid gameId, Guid issueId)
    {
        var participant = await _gameAccessService.GetRequiredParticipantAsync(gameId);
        await _issueService.GetAndValidateActiveIssueAsync(gameId, issueId);

        var vote = await _repoVotes.GetVoteAsync(issueId, participant.Id);
        if (vote != null)
        {
            _repoVotes.Delete(vote);
            await _repoVotes.SaveChangesAsync();
        }
    }
}

