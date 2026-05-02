using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Mappers;

public static class GameMapper
{
    public static Game ToGame(CreateGameRequestDto createGameRequestDto) =>
        new()
        {
            Name = createGameRequestDto.Name,
            VotingSystem = createGameRequestDto.VotingSystem,
            AutoRevealCards = createGameRequestDto.AutoRevealCards,
            ShowAverage = createGameRequestDto.ShowAverage,
            ShowCountdownAnimation = createGameRequestDto.ShowCountdownAnimation,
            IsActive = true,
            IsDeleted = false
        };
    
    public static Game ToGame(UpdateGameRequestDto updateGameRequestDto) =>
        new()
        {
            Name = updateGameRequestDto.Name,
            VotingSystem = updateGameRequestDto.VotingSystem,
            AutoRevealCards = updateGameRequestDto.AutoRevealCards,
            ShowAverage = updateGameRequestDto.ShowAverage,
            ShowCountdownAnimation = updateGameRequestDto.ShowCountdownAnimation,
            IsActive = updateGameRequestDto.IsActive,
            IsDeleted = false
        };

    public static GameDto ToGameDto(Game game) =>
        new()
        {
            Id = game.Id,
            Name = game.Name,
            VotingSystem = game.VotingSystem,
            InviteCode = game.InviteCode,
            AutoRevealCards = game.AutoRevealCards,
            ShowAverage = game.ShowAverage,
            ShowCountdownAnimation = game.ShowCountdownAnimation,
            IsActive = game.IsActive,
            CreatedAt =  game.CreatedAt,
            CreatedBy = game.CreatedBy,
            Participants = (game.Participants ?? [])
                .Where(participant => participant.RemovedAt == null)
                .Select(ParticipantMapper.ToGameParticipantDto)
                .ToList(),
            Issues = (game.Issues ?? [])
                .Where(issue => !issue.IsRemoved)
                .Select(IssueMapper.ToDto)
                .ToList()
        };
}
