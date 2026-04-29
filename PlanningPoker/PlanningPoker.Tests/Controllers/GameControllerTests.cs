using Microsoft.AspNetCore.Mvc;
using Moq;
using PlanningPoker.API.Controllers;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Tests.Controllers;

public class GameControllerTests
{
    private readonly Mock<IGameService> _gameService = new();
    private readonly GameController _controller;

    public GameControllerTests()
    {
        _controller = new GameController(_gameService.Object);
    }

    [Fact]
    public async Task CreateGame_WhenRequestIsValid_ReturnsOkWithCreatedGame()
    {
        var request = CreateGameRequest("new-game", VotingSystem.Custom, true);
        var expectedGame = CreateGameDto("new-game", VotingSystem.Custom, true);

        _gameService.Setup(service => service.AddGameAsync(request)).ReturnsAsync(expectedGame);

        var result = await _controller.CreateGame(request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var gameDto = Assert.IsType<GameDto>(okResult.Value);

        Assert.Same(expectedGame, gameDto);
        _gameService.Verify(service => service.AddGameAsync(request), Times.Once);
    }

    [Fact]
    public async Task CreateGame_WhenGameNameAlreadyExists_ThrowsException()
    {
        var request = CreateGameRequest("new-game", VotingSystem.Custom, true);
        _gameService
            .Setup(service => service.AddGameAsync(request))
            .ThrowsAsync(new GameAlreadyExistsException(request.Name));

        var act = async () => await _controller.CreateGame(request);

        await Assert.ThrowsAsync<GameAlreadyExistsException>(act);
        _gameService.Verify(service => service.AddGameAsync(request), Times.Once);
    }

    [Fact]
    public async Task GetGame_WhenGameExists_ReturnsOkWithGame()
    {
        var gameId = Guid.NewGuid();
        var expectedGame = CreateGameDto("existing-game", VotingSystem.Fibonacci, false);

        _gameService.Setup(service => service.GetGameByIdAsync(gameId)).ReturnsAsync(expectedGame);

        var result = await _controller.GetGame(gameId);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var gameDto = Assert.IsType<GameDto>(okResult.Value);

        Assert.Same(expectedGame, gameDto);
        _gameService.Verify(service => service.GetGameByIdAsync(gameId), Times.Once);
    }

    [Fact]
    public async Task GetGame_WhenGameDoesNotExist_ThrowsException()
    {
        var gameId = Guid.NewGuid();
        _gameService
            .Setup(service => service.GetGameByIdAsync(gameId))
            .ThrowsAsync(new NotFoundException(nameof(Game), gameId));

        var act = async () => await _controller.GetGame(gameId);

        await Assert.ThrowsAsync<NotFoundException>(act);
        _gameService.Verify(service => service.GetGameByIdAsync(gameId), Times.Once);
    }

    [Fact]
    public async Task UpdateGame_WhenRequestIsValid_ReturnsOkWithUpdatedGame()
    {
        var gameId = Guid.NewGuid();
        var request = CreateUpdatedGameRequest("updated-game", VotingSystem.PowersOfTwo, false, false, false, false);
        var expectedGame = CreateGameDto(
            request.Name,
            request.VotingSystem,
            request.AutoRevealCards,
            request.ShowAverage,
            request.ShowCountdownAnimation,
            request.IsActive);

        _gameService.Setup(service => service.UpdateGameAsync(gameId, request)).ReturnsAsync(expectedGame);

        var result = await _controller.UpdateGame(gameId, request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var gameDto = Assert.IsType<GameDto>(okResult.Value);

        Assert.Same(expectedGame, gameDto);
        _gameService.Verify(service => service.UpdateGameAsync(gameId, request), Times.Once);
    }

    [Fact]
    public async Task UpdateGame_WhenUserDoesNotHaveRights_ThrowsException()
    {
        var gameId = Guid.NewGuid();
        var request = CreateUpdatedGameRequest("updated-game", VotingSystem.PowersOfTwo, false);
        _gameService
            .Setup(service => service.UpdateGameAsync(gameId, request))
            .ThrowsAsync(new ForbiddenException("update", "game"));

        var act = async () => await _controller.UpdateGame(gameId, request);

        await Assert.ThrowsAsync<ForbiddenException>(act);
        _gameService.Verify(service => service.UpdateGameAsync(gameId, request), Times.Once);
    }

    [Fact]
    public async Task DeleteGame_WhenGameIsNotDeleted_ReturnsNoContent()
    {
        var gameId = Guid.NewGuid();

        var result = await _controller.DeleteGame(gameId);

        Assert.IsType<NoContentResult>(result);
        _gameService.Verify(service => service.DeleteGameAsync(gameId), Times.Once);
    }

    [Fact]
    public async Task DeleteGame_WhenUserDoesNotHaveRights_ThrowsException()
    {
        var gameId = Guid.NewGuid();
        _gameService
            .Setup(service => service.DeleteGameAsync(gameId))
            .ThrowsAsync(new ForbiddenException("delete", "game"));

        var act = async () => await _controller.DeleteGame(gameId);

        await Assert.ThrowsAsync<ForbiddenException>(act);
        _gameService.Verify(service => service.DeleteGameAsync(gameId), Times.Once);
    }

    private static CreateGameRequestDto CreateGameRequest(string name, VotingSystem votingSystem, bool autoReveal)
    {
        return new CreateGameRequestDto
        {
            Name = name,
            VotingSystem = votingSystem,
            AutoRevealCards = autoReveal,
            ShowAverage = true,
            ShowCountdownAnimation = true
        };
    }

    private static UpdateGameRequestDto CreateUpdatedGameRequest(
        string name,
        VotingSystem votingSystem,
        bool autoReveal,
        bool showAverage = true,
        bool showCountdownAnimation = true,
        bool isActive = true)
    {
        return new UpdateGameRequestDto
        {
            Name = name,
            VotingSystem = votingSystem,
            AutoRevealCards = autoReveal,
            ShowAverage = showAverage,
            ShowCountdownAnimation = showCountdownAnimation,
            IsActive = isActive,
        };
    }

    private static GameDto CreateGameDto(
        string name,
        VotingSystem votingSystem,
        bool autoReveal,
        bool showAverage = true,
        bool showCountdownAnimation = true,
        bool isActive = true)
    {
        return new GameDto
        {
            Name = name,
            VotingSystem = votingSystem,
            AutoRevealCards = autoReveal,
            ShowAverage = showAverage,
            ShowCountdownAnimation = showCountdownAnimation,
            IsActive = isActive,
            CreatedBy = Guid.NewGuid(),
            Participants = new List<GameParticipantDto>()
        };
    }
}
