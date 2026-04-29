using Moq;
using PlanningPoker.BLL.Services;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Tests.Services;

public class GameServiceTests
{
    private readonly Mock<IGameRepository> _gameRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ICurrentUserService> _currentUserService = new();
    private readonly GameService _gameService;
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly Guid _masterId = Guid.NewGuid();

    public GameServiceTests()
    {
        _gameService = new GameService(_gameRepository.Object, _userRepository.Object, _currentUserService.Object);
    }

    [Fact]
    public async Task AddGameAsync_WhenInputIsValid_CreatesMasterParticipantAndGame()
    {
        var user = CreateUser(_masterId, "ScrumMaster");
        var request = CreateGameRequest("newGame", VotingSystem.Custom, true);

        SetupCurrentUser(_masterId);
        _userRepository.Setup(repository => repository.GetByIdAsync(_masterId)).ReturnsAsync(user);
        _gameRepository.Setup(repository => repository.ExistsByNameAsync(request.Name, _masterId)).ReturnsAsync(false);

        var result = await _gameService.AddGameAsync(request);

        Assert.Equal(request.Name, result.Name);
        Assert.Equal(request.VotingSystem, result.VotingSystem);
        Assert.Equal(request.AutoRevealCards, result.AutoRevealCards);
        Assert.Equal(_masterId, result.CreatedBy);
        Assert.True(result.IsActive);
        Assert.Single(result.Participants);

        var participant = Assert.Single(result.Participants);
        Assert.Equal(_masterId, participant.UserId);
        Assert.Equal("ScrumMaster", participant.DisplayName);
        Assert.Equal(ParticipantRole.Master, participant.Role);
        Assert.True(participant.IsConnected);

        _gameRepository.Verify(repository => repository.AddAsync(It.Is<Game>(game =>
            game.Name == request.Name &&
            game.VotingSystem == request.VotingSystem &&
            game.AutoRevealCards == request.AutoRevealCards &&
            game.ShowAverage == request.ShowAverage &&
            game.ShowCountdownAnimation == request.ShowCountdownAnimation &&
            game.CreatedBy == _masterId &&
            game.IsActive &&
            game.Participants.Count == 1 &&
            game.Participants.Single().UserId == _masterId &&
            game.Participants.Single().DisplayName == "ScrumMaster" &&
            game.Participants.Single().Role == ParticipantRole.Master &&
            game.Participants.Single().IsConnected)), Times.Once);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddGameAsync_WhenCurrentUserDoesNotExist_ThrowsNotFoundException()
    {
        var request = CreateGameRequest("newGame", VotingSystem.Custom, true);
        SetupCurrentUser(_masterId);
        _userRepository.Setup(repository => repository.GetByIdAsync(_masterId)).ReturnsAsync((User?)null);

        var act = async () => await _gameService.AddGameAsync(request);

        await Assert.ThrowsAsync<NotFoundException>(act);
        _gameRepository.Verify(repository => repository.AddAsync(It.IsAny<Game>()), Times.Never);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task AddGameAsync_WhenNameAlreadyExists_ThrowsGameAlreadyExistsException()
    {
        var request = CreateGameRequest("newGame", VotingSystem.Custom, true);

        SetupCurrentUser(_masterId);
        _userRepository.Setup(repository => repository.GetByIdAsync(_masterId)).ReturnsAsync(CreateUser(_masterId, "ScrumMaster"));
        _gameRepository.Setup(repository => repository.ExistsByNameAsync(request.Name, _masterId)).ReturnsAsync(true);

        var act = async () => await _gameService.AddGameAsync(request);

        await Assert.ThrowsAsync<GameAlreadyExistsException>(act);
        _gameRepository.Verify(repository => repository.AddAsync(It.IsAny<Game>()), Times.Never);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }
    
    [Fact]
    public async Task GetGameByIdAsync_WhenGameExists_ReturnsGame()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);
        
        var result = await _gameService.GetGameByIdAsync(game.Id);
        
        Assert.Equal(game.Name, result.Name);
        Assert.Equal(game.VotingSystem, result.VotingSystem);
        Assert.Equal(game.AutoRevealCards, result.AutoRevealCards);
        Assert.Equal(game.CreatedBy, result.CreatedBy);
    }
    
    [Fact]
    public async Task GetGameByIdAsync_WhenGameDoesNotExist_ThrowsNotFoundException()
    {
        var gameId = Guid.NewGuid();
        _gameRepository.Setup(repository => repository.GetByIdAsync(gameId)).ReturnsAsync((Game?)null);
        
        var act = async() => await _gameService.GetGameByIdAsync(gameId);
    
        await Assert.ThrowsAsync<NotFoundException>(act);
    }
    
    [Fact]
    public async Task UpdateGameAsync_WhenCurrentUserIsMaster_UpdatesGameAndPersistsChanges()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;
        game.Participants.Add(CreateParticipant(_masterId, ParticipantRole.Master));
        
        var updatedGame = CreateUpdatedGameRequest("updatedGame", VotingSystem.Fibonacci, false, false, false, false);

        SetupCurrentUser(_masterId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);
        _gameRepository
            .Setup(repository => repository.ExistsByNameAsync(updatedGame.Name, game.CreatedBy, game.Id))
            .ReturnsAsync(false);
        
        var result = await _gameService.UpdateGameAsync(game.Id, updatedGame);
        
        Assert.Equal(updatedGame.Name, result.Name);
        Assert.Equal(updatedGame.VotingSystem, result.VotingSystem);
        Assert.Equal(updatedGame.AutoRevealCards, result.AutoRevealCards);
        Assert.Equal(updatedGame.ShowAverage, result.ShowAverage);
        Assert.Equal(updatedGame.ShowCountdownAnimation, result.ShowCountdownAnimation);
        Assert.Equal(updatedGame.IsActive, result.IsActive);

        _gameRepository.Verify(repository => repository.Update(game), Times.Once);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateGameAsync_WhenGameDoesNotExist_ThrowsNotFoundException()
    {
        var gameId = Guid.NewGuid();
        var updatedGame = CreateUpdatedGameRequest("updatedGame", VotingSystem.Custom, true);

        SetupCurrentUser(_masterId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(gameId)).ReturnsAsync((Game?)null);

        var act = async () => await _gameService.UpdateGameAsync(gameId, updatedGame);

        await Assert.ThrowsAsync<NotFoundException>(act);
        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateGameAsync_WhenGameIsDeleted_ThrowsNotFoundException()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.IsDeleted = true;
        game.Participants.Add(CreateParticipant(_masterId, ParticipantRole.Master));
        var updatedGame = CreateUpdatedGameRequest("updatedGame", VotingSystem.Custom, true);

        SetupCurrentUser(_masterId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);

        var act = async () => await _gameService.UpdateGameAsync(game.Id, updatedGame);

        await Assert.ThrowsAsync<NotFoundException>(act);
        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
    }

    [Fact]
    public async Task UpdateGameAsync_WhenNameAlreadyExists_ThrowsGameAlreadyExistsException()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;
        game.Participants.Add(CreateParticipant(_masterId, ParticipantRole.Master));
        
        var updatedGame = CreateUpdatedGameRequest("updatedGame", VotingSystem.Custom, true);
        SetupCurrentUser(_masterId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);
        _gameRepository
            .Setup(repository => repository.ExistsByNameAsync(updatedGame.Name, game.CreatedBy, game.Id))
            .ReturnsAsync(true);
        
        var act = async() => await _gameService.UpdateGameAsync(game.Id, updatedGame);
        
        await Assert.ThrowsAsync<GameAlreadyExistsException>(act);
        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
    }

    [Fact]
    public async Task UpdateGameAsync_WhenCurrentUserIsNotMaster_ThrowsForbiddenException()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;
        game.Participants.Add(CreateParticipant(_playerId, ParticipantRole.Player));
        
        var updatedGame = CreateUpdatedGameRequest("updatedGame", VotingSystem.Custom, true);
        SetupCurrentUser(_playerId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);
        
        var act = async() => await _gameService.UpdateGameAsync(game.Id, updatedGame);
        
        await Assert.ThrowsAsync<ForbiddenException>(act);
        _gameRepository.Verify(
            repository => repository.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteGameAsync_WhenCurrentUserIsMaster_MarksGameAsDeleted()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.Participants.Add(CreateParticipant(_masterId, ParticipantRole.Master));
        SetupCurrentUser(_masterId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);

        await _gameService.DeleteGameAsync(game.Id);

        Assert.True(game.IsDeleted);
        Assert.False(game.IsActive);
        _gameRepository.Verify(repository => repository.Update(game), Times.Once);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }
    
    [Fact]
    public async Task DeleteGameAsync_WhenGameDoesNotExist_ReturnsWithoutSaving()
    {
        var gameId = Guid.NewGuid();
        SetupCurrentUser(_masterId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(gameId)).ReturnsAsync((Game?)null);

        await _gameService.DeleteGameAsync(gameId);

        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteGameAsync_WhenCurrentUserIsNotMaster_ThrowsForbiddenException()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.Participants.Add(CreateParticipant(_playerId, ParticipantRole.Player));
        SetupCurrentUser(_playerId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);

        var act = async() => await _gameService.DeleteGameAsync(game.Id);

        await Assert.ThrowsAsync<ForbiddenException>(act);
        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    private void SetupCurrentUser(Guid userId)
    {
        _currentUserService.Setup(service => service.GetRequiredUserId()).Returns(userId);
    }

    private static User CreateUser(Guid userId, string displayName)
    {
        return new User
        {
            Id = userId,
            DisplayName = displayName
        };
    }

    private static GameParticipant CreateParticipant(Guid userId, ParticipantRole role)
    {
        return new GameParticipant()
        {
            UserId = userId,
            DisplayName = $"Participant{userId}",
            Role = role
        };
    }

    private static Game CreateGame(string name, VotingSystem votingSystem, bool autoReveal)
    {
        return new Game()
        {
            Id = Guid.Parse("5a85a50c-e305-4e1e-bb11-47b689169a51"),
            Name = name,
            VotingSystem = votingSystem,
            InviteCode = "123",
            AutoRevealCards = autoReveal,
            ShowAverage = true,
            ShowCountdownAnimation = true,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Participants = new List<GameParticipant>()
        };
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
}
