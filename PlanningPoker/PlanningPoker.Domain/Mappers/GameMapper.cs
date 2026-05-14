using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Mappers;

public static class GameMapper
{
    public static Game ToGame(CreateGameRequestDto dto) =>
        new()
        {
            Name = dto.DisplayName,
            VotingSystem = dto.VotingSystem,
            CustomValues = dto.CustomValues,
            RevealPolicy = dto.RevealPolicy,
            IssuesPolicy = dto.IssuesPolicy,
            AutoRevealCards = dto.AutoRevealCards,
            DefaultTimerMinutes = dto.DefaultTimerMinutes,
            ShowAverage = dto.ShowAverage,
            ShowCountdownAnimation = dto.ShowCountdownAnimation,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };

    public static Game ToGame(UpdateGameRequestDto updateGameRequestDto) =>
        new()
        {
            Name = updateGameRequestDto.Name,
            VotingSystem = updateGameRequestDto.VotingSystem,
            RevealPolicy = updateGameRequestDto.RevealPolicy,
            IssuesPolicy = updateGameRequestDto.IssuesPolicy,
            AutoRevealCards = updateGameRequestDto.AutoRevealCards,
            ShowAverage = updateGameRequestDto.ShowAverage,
            ShowCountdownAnimation = updateGameRequestDto.ShowCountdownAnimation,
            EnableFunFeatures = updateGameRequestDto.EnableFunFeatures,
            IsActive = updateGameRequestDto.IsActive,
            DefaultTimerMinutes = updateGameRequestDto.DefaultTimerMinutes,
            AutoResetTimer = updateGameRequestDto.AutoResetTimer,
            IsDeleted = false
        };


    public static GameDto ToGameDto(Game game) =>
        new()
        {
            Id = game.Id,
            Name = game.Name,
            InviteCode = game.InviteCode,
            VotingSystem = game.VotingSystem,
            CustomValues = game.CustomValues,
            RevealPolicy = game.RevealPolicy,
            IssuesPolicy = game.IssuesPolicy,
            ShowAverage = game.ShowAverage,
            ShowCountdownAnimation = game.ShowCountdownAnimation,
            EnableFunFeatures = game.EnableFunFeatures,
            DefaultTimerMinutes = game.DefaultTimerMinutes,
            IsActive = game.IsActive,
            CreatedAt = game.CreatedAt,
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

    public static UserGameDto ToUserGameDto(Game game, Guid currentUserId) =>
        new()
        {
            Id = game.Id.ToString(),
            Name = game.Name,
            JoinedAt = GetJoinedAt(game, currentUserId),
            SessionRole = GetSessionRole(game, currentUserId),
            IsActive = game.IsActive,
            IsDeleted = game.IsDeleted
        };

    private static DateTime GetJoinedAt(Game game, Guid currentUserId)
    {
        var currentParticipant = GetCurrentUserParticipant(game, currentUserId);
        return currentParticipant?.JoinedAt ?? game.CreatedAt;
    }

    private static ParticipantRole GetSessionRole(Game game, Guid currentUserId)
    {
        var currentParticipant = GetCurrentUserParticipant(game, currentUserId);
        if (currentParticipant is not null)
        {
            return currentParticipant.Role;
        }

        return game.CreatedBy == currentUserId
            ? ParticipantRole.Master
            : ParticipantRole.Player;
    }

    private static GameParticipant? GetCurrentUserParticipant(Game game, Guid currentUserId)
    {
        return (game.Participants)
            .FirstOrDefault(participant =>
                participant.UserId == currentUserId &&
                participant.RemovedAt == null);
    }
}
