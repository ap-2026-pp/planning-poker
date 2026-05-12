using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Mappers;

public static class GameMapper
{
    public static Game ToGame(CreateGameRequestDto dto) =>
        new()
        {
            Name = dto.Name,
            VotingSystem = dto.VotingSystem,
            CustomValues = dto.CustomValues, 
            RevealPolicy = RevealPolicy.MasterOnly,
            IssuesPolicy = IssuesPolicy.MasterOnly,
            AutoRevealCards = dto.AutoRevealCards,
            ShowAverage = dto.ShowAverage,
            ShowCountdownAnimation = dto.ShowCountdownAnimation,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };


    public static GameDto ToGameDto(Game game) =>
        new()
        {
            Id = game.Id,
            Name = game.Name,
            InviteCode = game.InviteCode,
            VotingSystem = game.VotingSystem.ToString(),
            CustomValues = game.CustomValues, 
            RevealPolicy = game.RevealPolicy.ToString(),
            AutoRevealCards = game.AutoRevealCards,
            ShowAverage = game.ShowAverage,
            ShowCountdownAnimation = game.ShowCountdownAnimation,
            DefaultTimerMinutes = game.DefaultTimerMinutes,
            IsActive = game.IsActive,
            CreatedAt =  game.CreatedAt,
            CreatedBy = game.CreatedBy,
            Participants = (game.Participants ?? [])
                .Where(participant => participant.RemovedAt == null)
                .Select(ParticipantMapper.ToGameParticipantDto)
                .ToList(),
            Issues = (game.Issues ?? [])
                .Where(issue => !issue.IsRemoved)
                .Select(issue => IssueMapper.ToDto(issue, issue.VotingResults?.FirstOrDefault()))
                .ToList()
        };
}
