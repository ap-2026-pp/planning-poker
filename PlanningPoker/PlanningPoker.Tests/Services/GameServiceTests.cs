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
    private readonly Mock<ICurrentUserContext> _currentUserContext = new();
    private readonly GameService _gameService;
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly Guid _masterId = Guid.NewGuid();

    public GameServiceTests()
    {
        _gameService = new GameService(_gameRepository.Object, _currentUserContext.Object);
    }

    [Fact]
    public async Task AddGameAsync_WhenHostDisplayNameIsProvided_UsesRequestValue()
    {
        var request = CreateGameRequest("newGame", "ScrumMaster", VotingSystem.Custom, true);
        var user = CreateUser(_masterId, "Default From Db");

        SetupCurrentUser(user);
        _gameRepository.Setup(repository => repository.ExistsByNameAsync(request.Name, _masterId)).ReturnsAsync(false);
        _gameRepository.Setup(repository => repository.ExistsByInviteCodeAsync(It.IsAny<string>())).ReturnsAsync(false);

        var result = await _gameService.AddGameAsync(request);

        Assert.Equal(request.Name, result.Name);
        Assert.Equal(request.VotingSystem, result.VotingSystem);
        Assert.Equal(request.AutoRevealCards, result.AutoRevealCards);
        Assert.Equal(_masterId, result.CreatedBy);
        Assert.True(result.IsActive);
        Assert.NotNull(result.InviteCode);
        Assert.Equal(20, result.InviteCode.Length);
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
            game.InviteCode.Length == 20 &&
            game.Participants.Count == 1 &&
            game.Participants.Single().UserId == _masterId &&
            game.Participants.Single().DisplayName == "ScrumMaster" &&
            game.Participants.Single().Role == ParticipantRole.Master &&
            game.Participants.Single().IsConnected)), Times.Once);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddGameAsync_WhenHostDisplayNameIsMissing_UsesCurrentUserDisplayNameFromDb()
    {
        var request = CreateGameRequest("newGame", null, VotingSystem.Custom, true);
        var user = CreateUser(_masterId, "Default From Db");

        SetupCurrentUser(user);
        _gameRepository.Setup(repository => repository.ExistsByNameAsync(request.Name, _masterId)).ReturnsAsync(false);
        _gameRepository.Setup(repository => repository.ExistsByInviteCodeAsync(It.IsAny<string>())).ReturnsAsync(false);

        var result = await _gameService.AddGameAsync(request);

        var participant = Assert.Single(result.Participants);
        Assert.Equal("Default From Db", participant.DisplayName);
    }

    [Fact]
    public async Task AddGameAsync_WhenCurrentUserDoesNotExist_ThrowsNotFoundException()
    {
        var request = CreateGameRequest("newGame", null, VotingSystem.Custom, true);

        _currentUserContext
            .Setup(context => context.GetRequiredUserAsync())
            .ThrowsAsync(new NotFoundException(nameof(User), _masterId));

        var act = async () => await _gameService.AddGameAsync(request);

        await Assert.ThrowsAsync<NotFoundException>(act);
        _gameRepository.Verify(repository => repository.AddAsync(It.IsAny<Game>()), Times.Never);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task AddGameAsync_WhenNameAlreadyExists_ThrowsResourceAlreadyExistsException()
    {
        var request = CreateGameRequest("newGame", "ScrumMaster", VotingSystem.Custom, true);

        SetupCurrentUser(CreateUser(_masterId, "Default From Db"));
        _gameRepository.Setup(repository => repository.ExistsByNameAsync(request.Name, _masterId)).ReturnsAsync(true);

        var act = async () => await _gameService.AddGameAsync(request);

        await Assert.ThrowsAsync<ResourceAlreadyExistsException>(act);
        _gameRepository.Verify(repository => repository.AddAsync(It.IsAny<Game>()), Times.Never);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task GetGameByIdAsync_WhenGameExists_ReturnsGame()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);

        var result = await _gameService.GetGameByIdAsync(game.Id);

        Assert.Equal(game.Id, result.Id);
        Assert.Equal(game.Name, result.Name);
        Assert.Equal(game.InviteCode, result.InviteCode);
        Assert.Equal(game.CreatedBy, result.CreatedBy);
    }

    [Fact]
    public async Task GetGameByIdAsync_WhenGameCollectionsAreNull_ReturnsEmptyCollections()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.Participants = null!;
        game.Issues = null!;

        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);

        var result = await _gameService.GetGameByIdAsync(game.Id);

        Assert.NotNull(result.Participants);
        Assert.NotNull(result.Issues);
        Assert.Empty(result.Participants);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task GetGameByIdAsync_WhenGameDoesNotExist_ThrowsNotFoundException()
    {
        var gameId = Guid.NewGuid();
        _gameRepository.Setup(repository => repository.GetByIdAsync(gameId)).ReturnsAsync((Game?)null);

        var act = async () => await _gameService.GetGameByIdAsync(gameId);

        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task GetGameInviteAsync_WhenCurrentUserIsParticipant_ReturnsGame()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.Participants.Add(CreateParticipant(_masterId, ParticipantRole.Master));

        SetupCurrentUserId(_masterId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);

        var result = await _gameService.GetGameInviteAsync(game.Id);

        Assert.Same(game, result);
    }

    [Fact]
    public async Task GetGameInviteAsync_WhenCurrentUserIsNotParticipant_ThrowsForbiddenException()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.Participants.Add(CreateParticipant(_masterId, ParticipantRole.Master));

        SetupCurrentUserId(_playerId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);

        var act = async () => await _gameService.GetGameInviteAsync(game.Id);

        await Assert.ThrowsAsync<ForbiddenException>(act);
    }

    [Fact]
    public async Task UpdateGameAsync_WhenCurrentUserIsMaster_UpdatesGameAndPersistsChanges()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;
        game.Participants.Add(CreateParticipant(_masterId, ParticipantRole.Master));

        var updatedGame = CreateUpdatedGameRequest("updatedGame", VotingSystem.Fibonacci, false, false, false, false);

        SetupCurrentUserId(_masterId);
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
    public async Task UpdateGameAsync_WhenNameAlreadyExists_ThrowsResourceAlreadyExistsException()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;
        game.Participants.Add(CreateParticipant(_masterId, ParticipantRole.Master));

        var updatedGame = CreateUpdatedGameRequest("updatedGame", VotingSystem.Custom, true);
        SetupCurrentUserId(_masterId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);
        _gameRepository
            .Setup(repository => repository.ExistsByNameAsync(updatedGame.Name, game.CreatedBy, game.Id))
            .ReturnsAsync(true);

        var act = async () => await _gameService.UpdateGameAsync(game.Id, updatedGame);

        await Assert.ThrowsAsync<ResourceAlreadyExistsException>(act);
        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
    }

    [Fact]
    public async Task UpdateGameAsync_WhenCurrentUserIsNotMaster_ThrowsForbiddenException()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;
        game.Participants.Add(CreateParticipant(_playerId, ParticipantRole.Player));

        var updatedGame = CreateUpdatedGameRequest("updatedGame", VotingSystem.Custom, true);
        SetupCurrentUserId(_playerId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);

        var act = async () => await _gameService.UpdateGameAsync(game.Id, updatedGame);

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
        SetupCurrentUserId(_masterId);
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
        SetupCurrentUserId(_masterId);
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
        SetupCurrentUserId(_playerId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);

        var act = async () => await _gameService.DeleteGameAsync(game.Id);

        await Assert.ThrowsAsync<ForbiddenException>(act);
        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    private void SetupCurrentUser(User user)
    {
        _currentUserContext.Setup(context => context.GetRequiredUserAsync()).ReturnsAsync(user);
        _currentUserContext.Setup(context => context.GetRequiredUserId()).Returns(user.Id);
    }

    private void SetupCurrentUserId(Guid userId)
    {
        _currentUserContext.Setup(context => context.GetRequiredUserId()).Returns(userId);
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
        return new GameParticipant
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DisplayName = $"Participant{userId}",
            Role = role,
            IsConnected = true,
            JoinedAt = DateTime.UtcNow
        };
    }

    private static Game CreateGame(string name, VotingSystem votingSystem, bool autoReveal)
    {
        return new Game
        {
            Id = Guid.Parse("5a85a50c-e305-4e1e-bb11-47b689169a51"),
            Name = name,
            VotingSystem = votingSystem,
            InviteCode = "abc123INVITECODE9876",
            AutoRevealCards = autoReveal,
            ShowAverage = true,
            ShowCountdownAnimation = true,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Participants = new List<GameParticipant>()
        };
    }

    private static CreateGameRequestDto CreateGameRequest(
        string name,
        string? hostDisplayName,
        VotingSystem votingSystem,
        bool autoReveal)
    {
        return new CreateGameRequestDto
        {
            Name = name,
            HostDisplayName = hostDisplayName,
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
