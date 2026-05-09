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
            RevealPolicy = createGameRequestDto.RevealPolicy,
            IssuesPolicy = createGameRequestDto.IssuesPolicy,
            AutoRevealCards = createGameRequestDto.AutoRevealCards,
            ShowAverage = createGameRequestDto.ShowAverage,
            ShowCountdownAnimation = createGameRequestDto.ShowCountdownAnimation,
            EnableFunFeatures = createGameRequestDto.EnableFunFeatures,
            IsActive = true,
            IsDeleted = false
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
            IsDeleted = false
        };

    public static GameDto ToGameDto(Game game) =>
        new()
        {
            Id = game.Id,
            Name = game.Name,
            VotingSystem = game.VotingSystem,
            InviteCode = game.InviteCode,
            RevealPolicy = game.RevealPolicy,
            IssuesPolicy = game.IssuesPolicy,
            AutoRevealCards = game.AutoRevealCards,
            ShowAverage = game.ShowAverage,
            ShowCountdownAnimation = game.ShowCountdownAnimation,
            EnableFunFeatures = game.EnableFunFeatures,
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
