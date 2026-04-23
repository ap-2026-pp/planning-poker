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
    
    public static Game ToGame(GameDto createGameDto) =>
        new()
        {
            Name = createGameDto.Name,
            VotingSystem = createGameDto.VotingSystem,
            AutoRevealCards = createGameDto.AutoRevealCards,
            ShowAverage = createGameDto.ShowAverage,
            ShowCountdownAnimation = createGameDto.ShowCountdownAnimation,
            IsActive = createGameDto.IsActive,
            InviteCode = "123",
            CreatedBy = createGameDto.CreatedBy.GetValueOrDefault()
        };
}
