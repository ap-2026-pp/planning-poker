using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Mappers;

public class GameMapper
{
    public static GameDto ToGameDto(Game game) =>
        new()
        {
            Name = game.Name,
            VotingSystem = game.VotingSystem,
            AutoRevealCards = game.AutoRevealCards,
            ShowAverage = game.ShowAverage,
            ShowCountdownAnimation = game.ShowCountdownAnimation,
            IsActive = game.IsActive,
            CreatedBy = game.CreatedBy
        };

    public static Game ToGame(GameDto gameDto) =>
        new()
        {
            Name = gameDto.Name,
            VotingSystem = gameDto.VotingSystem,
            AutoRevealCards = gameDto.AutoRevealCards,
            ShowAverage = gameDto.ShowAverage,
            ShowCountdownAnimation = gameDto.ShowCountdownAnimation,
            IsActive = gameDto.IsActive,
            CreatedBy = gameDto.CreatedBy.GetValueOrDefault()
        };
}
