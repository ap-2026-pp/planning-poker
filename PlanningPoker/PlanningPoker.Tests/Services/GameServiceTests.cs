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
    private readonly Mock<IParticipantRepository> _participantRepository = new();
    private readonly Mock<ICurrentUserContext> _currentUserContext = new();
    private readonly Mock<IGameRealtimeService> _gameRealtimeService = new();
    private readonly GameService _gameService;
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly Guid _masterId = Guid.NewGuid();

    public GameServiceTests()
    {
        var gameAccessService = new GameAccessService(
            _participantRepository.Object,
            _currentUserContext.Object);

        _gameService = new GameService(
            _gameRepository.Object,
            _currentUserContext.Object,
            gameAccessService,
            _gameRealtimeService.Object);
    }

    [Fact]
    public async Task AddGameAsync_WhenHostDisplayNameIsProvided_UsesRequestValue()
    {
        var request = CreateGameRequest("newGame", "ScrumMaster", VotingSystem.Custom, true);
        var user = CreateUser(_masterId, "Default From Db");

        SetupCurrentUser(user);

        _gameRepository
            .Setup(repository => repository.ExistsByNameAsync(request.DisplayName, _masterId))
            .ReturnsAsync(false);

        _gameRepository
            .Setup(repository => repository.ExistsByInviteCodeAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        var result = await _gameService.AddGameAsync(request);

        Assert.Equal(request.DisplayName, result.Name);
        Assert.Equal(request.VotingSystem, result.VotingSystem);
        Assert.Equal(request.RevealPolicy, result.RevealPolicy);
        Assert.Equal(request.IssuesPolicy, result.IssuesPolicy);
        Assert.Equal(request.AutoRevealCards, result.AutoRevealCards);
        Assert.Equal(request.EnableFunFeatures, result.EnableFunFeatures);
        Assert.Equal(_masterId, result.CreatedBy);
        Assert.True(result.IsActive);
        Assert.NotNull(result.InviteCode);
        Assert.Equal(20, result.InviteCode.Length);
        Assert.NotNull(result.Participants);

        var participant = Assert.Single(result.Participants!);

        Assert.Equal(_masterId, participant.UserId);
        Assert.Equal("ScrumMaster", participant.DisplayName);
        Assert.Equal(ParticipantRole.Master, participant.Role);
        Assert.True(participant.IsConnected);

        _gameRepository.Verify(repository => repository.AddAsync(It.Is<Game>(game =>
            game.Name == request.DisplayName &&
            game.VotingSystem == request.VotingSystem &&
            game.RevealPolicy == request.RevealPolicy &&
            game.IssuesPolicy == request.IssuesPolicy &&
            game.AutoRevealCards == request.AutoRevealCards &&
            game.ShowAverage == request.ShowAverage &&
            game.ShowCountdownAnimation == request.ShowCountdownAnimation &&
            game.EnableFunFeatures == request.EnableFunFeatures &&
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

        _gameRepository
            .Setup(repository => repository.ExistsByNameAsync(request.DisplayName, _masterId))
            .ReturnsAsync(false);

        _gameRepository
            .Setup(repository => repository.ExistsByInviteCodeAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        var result = await _gameService.AddGameAsync(request);

        Assert.NotNull(result.Participants);

        var participant = Assert.Single(result.Participants!);

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

        _gameRepository
            .Setup(repository => repository.ExistsByNameAsync(request.DisplayName, _masterId))
            .ReturnsAsync(true);

        var act = async () => await _gameService.AddGameAsync(request);

        await Assert.ThrowsAsync<ResourceAlreadyExistsException>(act);

        _gameRepository.Verify(repository => repository.AddAsync(It.IsAny<Game>()), Times.Never);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task GetGameByIdAsync_WhenGameExists_ReturnsGame()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;

        var masterParticipant = CreateParticipant(_masterId, ParticipantRole.Master, game: game);
        game.Participants.Add(masterParticipant);

        SetupCurrentUserId(_masterId);

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(game.Id))
            .ReturnsAsync(game);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, _masterId, null))
            .ReturnsAsync(masterParticipant);

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
        game.CreatedBy = _masterId;

        var masterParticipant = CreateParticipant(_masterId, ParticipantRole.Master, game: game);
        game.Participants.Add(masterParticipant);
        game.Issues = null!;

        SetupCurrentUserId(_masterId);

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(game.Id))
            .ReturnsAsync(game);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, _masterId, null))
            .ReturnsAsync(masterParticipant);

        var result = await _gameService.GetGameByIdAsync(game.Id);

        Assert.NotNull(result.Participants);
        Assert.NotNull(result.Issues);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task GetGameByIdAsync_WhenGameDoesNotExist_ThrowsNotFoundException()
    {
        var gameId = Guid.NewGuid();

        SetupCurrentUserId(_masterId);

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(gameId))
            .ReturnsAsync((Game?)null);

        var act = async () => await _gameService.GetGameByIdAsync(gameId);

        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task GetGameByIdAsync_WhenCurrentUserIsNotParticipant_ThrowsForbiddenException()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;
        game.Participants.Add(CreateParticipant(_masterId, ParticipantRole.Master, game: game));

        SetupCurrentUserId(_playerId);

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(game.Id))
            .ReturnsAsync(game);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, _playerId, null))
            .ReturnsAsync((GameParticipant?)null);

        var act = async () => await _gameService.GetGameByIdAsync(game.Id);

        await Assert.ThrowsAsync<ForbiddenException>(act);
    }

    [Fact]
    public async Task GetGameInviteAsync_WhenCurrentUserIsParticipant_ReturnsGame()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;

        var masterParticipant = CreateParticipant(_masterId, ParticipantRole.Master, game: game);
        game.Participants.Add(masterParticipant);

        SetupCurrentUserId(_masterId);

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(game.Id))
            .ReturnsAsync(game);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, _masterId, null))
            .ReturnsAsync(masterParticipant);

        var result = await _gameService.GetGameInviteAsync(game.Id);

        Assert.Same(game, result);
    }

    [Fact]
    public async Task GetGameInviteAsync_WhenCurrentUserIsNotParticipant_ThrowsForbiddenException()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;
        game.Participants.Add(CreateParticipant(_masterId, ParticipantRole.Master, game: game));

        SetupCurrentUserId(_playerId);

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(game.Id))
            .ReturnsAsync(game);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, _playerId, null))
            .ReturnsAsync((GameParticipant?)null);

        var act = async () => await _gameService.GetGameInviteAsync(game.Id);

        await Assert.ThrowsAsync<ForbiddenException>(act);
    }

    [Fact]
    public async Task UpdateGameAsync_WhenCurrentParticipantIsGuestMaster_UpdatesGameAndPersistsChanges()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;

        var guestMaster = CreateGuestParticipant(ParticipantRole.Master, game);
        game.Participants.Add(guestMaster);

        var updatedGame = CreateUpdatedGameRequest(
            "updatedGame",
            VotingSystem.Fibonacci,
            false,
            false,
            false,
            false);

        SetupGuestIdentity(guestMaster.Id);

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(game.Id))
            .ReturnsAsync(game);

        _gameRepository
            .Setup(repository => repository.ExistsByNameAsync(updatedGame.Name, game.CreatedBy, game.Id))
            .ReturnsAsync(false);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, null, guestMaster.Id))
            .ReturnsAsync(guestMaster);

        var result = await _gameService.UpdateGameAsync(game.Id, updatedGame);

        Assert.Equal(updatedGame.Name, result.Name);
        Assert.Equal(updatedGame.VotingSystem, result.VotingSystem);

        _gameRepository.Verify(repository => repository.Update(game), Times.Once);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
        _gameRealtimeService.Verify(service => service.NotifyGameUpdatedAsync(game), Times.Once);
    }

    [Fact]
    public async Task DeleteGameAsync_WhenCurrentParticipantIsGuestMaster_MarksGameAsDeleted()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);

        var guestMaster = CreateGuestParticipant(ParticipantRole.Master, game);
        game.Participants.Add(guestMaster);

        SetupGuestIdentity(guestMaster.Id);

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(game.Id))
            .ReturnsAsync(game);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, null, guestMaster.Id))
            .ReturnsAsync(guestMaster);

        await _gameService.DeleteGameAsync(game.Id);

        Assert.True(game.IsDeleted);
        Assert.False(game.IsActive);

        _gameRepository.Verify(repository => repository.Update(game), Times.Once);
    }

    [Fact]
    public async Task GetGameInviteAsync_WhenCurrentParticipantIsGuestMaster_ReturnsGame()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);

        var guestMaster = CreateGuestParticipant(ParticipantRole.Master, game);
        game.Participants.Add(guestMaster);

        SetupGuestIdentity(guestMaster.Id);

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(game.Id))
            .ReturnsAsync(game);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, null, guestMaster.Id))
            .ReturnsAsync(guestMaster);

        var result = await _gameService.GetGameInviteAsync(game.Id);

        Assert.Same(game, result);
    }

    [Fact]
    public async Task UpdateGameAsync_WhenCurrentUserIsMaster_UpdatesGameAndPersistsChanges()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;

        var masterParticipant = CreateParticipant(_masterId, ParticipantRole.Master, game: game);
        game.Participants.Add(masterParticipant);

        var updatedGame = CreateUpdatedGameRequest(
            "updatedGame",
            VotingSystem.Fibonacci,
            false,
            false,
            false,
            false);

        SetupCurrentUserId(_masterId);

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(game.Id))
            .ReturnsAsync(game);

        _gameRepository
            .Setup(repository => repository.ExistsByNameAsync(updatedGame.Name, game.CreatedBy, game.Id))
            .ReturnsAsync(false);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, _masterId, null))
            .ReturnsAsync(masterParticipant);

        var result = await _gameService.UpdateGameAsync(game.Id, updatedGame);

        Assert.Equal(updatedGame.Name, result.Name);
        Assert.Equal(updatedGame.VotingSystem, result.VotingSystem);
        Assert.Equal(updatedGame.RevealPolicy, result.RevealPolicy);
        Assert.Equal(updatedGame.IssuesPolicy, result.IssuesPolicy);
        Assert.Equal(updatedGame.AutoRevealCards, result.AutoRevealCards);
        Assert.Equal(updatedGame.ShowAverage, result.ShowAverage);
        Assert.Equal(updatedGame.ShowCountdownAnimation, result.ShowCountdownAnimation);
        Assert.Equal(updatedGame.EnableFunFeatures, result.EnableFunFeatures);
        Assert.Equal(updatedGame.IsActive, result.IsActive);

        _gameRepository.Verify(repository => repository.Update(game), Times.Once);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
        _gameRealtimeService.Verify(service => service.NotifyGameUpdatedAsync(game), Times.Once);
    }

    [Fact]
    public async Task UpdateGameAsync_WhenNameAlreadyExists_ThrowsResourceAlreadyExistsException()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;

        var masterParticipant = CreateParticipant(_masterId, ParticipantRole.Master, game: game);
        game.Participants.Add(masterParticipant);

        var updatedGame = CreateUpdatedGameRequest("updatedGame", VotingSystem.Custom, true);

        SetupCurrentUserId(_masterId);

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(game.Id))
            .ReturnsAsync(game);

        _gameRepository
            .Setup(repository => repository.ExistsByNameAsync(updatedGame.Name, game.CreatedBy, game.Id))
            .ReturnsAsync(true);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, _masterId, null))
            .ReturnsAsync(masterParticipant);

        var act = async () => await _gameService.UpdateGameAsync(game.Id, updatedGame);

        await Assert.ThrowsAsync<ResourceAlreadyExistsException>(act);

        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
    }

    [Fact]
    public async Task UpdateGameAsync_WhenCurrentUserIsNotMaster_ThrowsForbiddenException()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;

        var playerParticipant = CreateParticipant(_playerId, ParticipantRole.Player, game: game);
        game.Participants.Add(playerParticipant);

        var updatedGame = CreateUpdatedGameRequest("updatedGame", VotingSystem.Custom, true);

        SetupCurrentUserId(_playerId);

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(game.Id))
            .ReturnsAsync(game);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, _playerId, null))
            .ReturnsAsync(playerParticipant);

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
        game.CreatedBy = _masterId;

        var masterParticipant = CreateParticipant(_masterId, ParticipantRole.Master, game: game);
        game.Participants.Add(masterParticipant);

        SetupCurrentUserId(_masterId);

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(game.Id))
            .ReturnsAsync(game);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, _masterId, null))
            .ReturnsAsync(masterParticipant);

        await _gameService.DeleteGameAsync(game.Id);

        Assert.True(game.IsDeleted);
        Assert.False(game.IsActive);

        _gameRepository.Verify(repository => repository.Update(game), Times.Once);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
        _gameRealtimeService.Verify(service => service.NotifyGameUpdatedAsync(game), Times.Once);
    }

    [Fact]
    public async Task DeleteGameAsync_WhenGameDoesNotExist_ReturnsWithoutSaving()
    {
        var gameId = Guid.NewGuid();

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(gameId))
            .ReturnsAsync((Game?)null);

        await _gameService.DeleteGameAsync(gameId);

        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteGameAsync_WhenCurrentUserIsNotMaster_ThrowsForbiddenException()
    {
        var game = CreateGame("newGame", VotingSystem.Custom, true);
        game.CreatedBy = _masterId;

        var playerParticipant = CreateParticipant(_playerId, ParticipantRole.Player, game: game);
        game.Participants.Add(playerParticipant);

        SetupCurrentUserId(_playerId);

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(game.Id))
            .ReturnsAsync(game);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, _playerId, null))
            .ReturnsAsync(playerParticipant);

        var act = async () => await _gameService.DeleteGameAsync(game.Id);

        await Assert.ThrowsAsync<ForbiddenException>(act);

        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
        _gameRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task GetUserGamesAsync_WhenScopeIsAll_ReturnsMappedUserGames()
    {
        var createdGame = CreateGame("created", VotingSystem.Custom, true);
        createdGame.CreatedBy = _masterId;
        createdGame.IsActive = true;
        createdGame.CreatedAt = DateTime.UtcNow.AddDays(-2);

        var createdParticipant = CreateParticipant(
            _masterId,
            ParticipantRole.Master,
            createdGame.CreatedAt.AddMinutes(5),
            createdGame);

        createdGame.Participants.Add(createdParticipant);

        var participatedGame = CreateGame("participated", VotingSystem.Custom, true);
        participatedGame.Id = Guid.NewGuid();
        participatedGame.CreatedBy = Guid.NewGuid();
        participatedGame.IsActive = false;
        participatedGame.CreatedAt = DateTime.UtcNow.AddDays(-1);

        var participatedParticipant = CreateParticipant(
            _masterId,
            ParticipantRole.Spectator,
            participatedGame.CreatedAt.AddMinutes(15),
            participatedGame);

        participatedGame.Participants.Add(participatedParticipant);

        SetupCurrentUserId(_masterId);

        _gameRepository
            .Setup(repository => repository.GetAllByUserId(_masterId))
            .ReturnsAsync(new List<Game> { createdGame, participatedGame });

        var result = (await _gameService.GetUserGamesAsync(UserGamesScope.All))!.ToList();

        Assert.Equal(2, result.Count);

        var createdResult = Assert.Single(result, game => game.Id == createdGame.Id.ToString());

        Assert.Equal("created", createdResult.Name);
        Assert.Equal(createdGame.Participants.Single().JoinedAt, createdResult.JoinedAt);
        Assert.Equal(ParticipantRole.Master, createdResult.SessionRole);
        Assert.True(createdResult.IsActive);

        var participatedResult = Assert.Single(result, game => game.Id == participatedGame.Id.ToString());

        Assert.Equal("participated", participatedResult.Name);
        Assert.Equal(participatedGame.Participants.Single().JoinedAt, participatedResult.JoinedAt);
        Assert.Equal(ParticipantRole.Spectator, participatedResult.SessionRole);
        Assert.False(participatedResult.IsActive);
    }

    private void SetupCurrentUser(User user)
    {
        _currentUserContext
            .Setup(context => context.GetCurrentParticipantIdentity())
            .Returns(new CurrentParticipantIdentity(user.Id, null));

        _currentUserContext
            .Setup(context => context.GetUserOrDefaultAsync())
            .ReturnsAsync(user);

        _currentUserContext
            .Setup(context => context.GetRequiredUserAsync())
            .ReturnsAsync(user);

        _currentUserContext
            .Setup(context => context.GetRequiredUserId())
            .Returns(user.Id);
    }

    private void SetupCurrentUserId(Guid userId)
    {
        _currentUserContext
            .Setup(context => context.GetCurrentParticipantIdentity())
            .Returns(new CurrentParticipantIdentity(userId, null));

        _currentUserContext
            .Setup(context => context.GetRequiredUserId())
            .Returns(userId);
    }

    private void SetupGuestIdentity(Guid participantId)
    {
        _currentUserContext
            .Setup(context => context.GetCurrentParticipantIdentity())
            .Returns(new CurrentParticipantIdentity(null, participantId));
    }

    private static User CreateUser(Guid userId, string displayName)
    {
        return new User
        {
            Id = userId,
            DisplayName = displayName
        };
    }

    private static GameParticipant CreateParticipant(
        Guid userId,
        ParticipantRole role,
        DateTime? joinedAt = null,
        Game? game = null)
    {
        return new GameParticipant
        {
            Id = Guid.NewGuid(),
            GameId = game?.Id ?? Guid.Empty,
            Game = game!,
            UserId = userId,
            DisplayName = $"Participant{userId}",
            Role = role,
            IsConnected = true,
            JoinedAt = joinedAt ?? DateTime.UtcNow
        };
    }

    private static GameParticipant CreateGuestParticipant(ParticipantRole role, Game? game = null)
    {
        return new GameParticipant
        {
            Id = Guid.NewGuid(),
            GameId = game?.Id ?? Guid.Empty,
            Game = game!,
            UserId = null,
            DisplayName = "Guest",
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
            CustomValues = votingSystem == VotingSystem.Custom ? "1, 2, 3, 5, 8" : null,
            InviteCode = "abc123INVITECODE9876",
            RevealPolicy = RevealPolicy.MasterOnly,
            IssuesPolicy = IssuesPolicy.MasterOnly,
            AutoRevealCards = autoReveal,
            ShowAverage = true,
            ShowCountdownAnimation = true,
            EnableFunFeatures = false,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsDeleted = false,
            DefaultTimerMinutes = 5,
            AutoResetTimer = false,
            Participants = new List<GameParticipant>(),
            Issues = new List<Issue>()
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
            DisplayName = hostDisplayName,
            VotingSystem = votingSystem,
            CustomValues = votingSystem == VotingSystem.Custom ? "1, 2, 3, 5, 8" : null,
            RevealPolicy = RevealPolicy.Everyone,
            IssuesPolicy = IssuesPolicy.Everyone,
            AutoRevealCards = autoReveal,
            AutoResetTimer = false,
            ShowAverage = true,
            ShowCountdownAnimation = true,
            EnableFunFeatures = true,
            DefaultTimerMinutes = 5
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
            CustomValues = votingSystem == VotingSystem.Custom ? "1, 2, 3, 5, 8" : null,
            RevealPolicy = RevealPolicy.Everyone,
            IssuesPolicy = IssuesPolicy.Everyone,
            AutoRevealCards = autoReveal,
            AutoResetTimer = false,
            ShowAverage = showAverage,
            ShowCountdownAnimation = showCountdownAnimation,
            EnableFunFeatures = true,
            IsActive = isActive,
            DefaultTimerMinutes = 5,
            RevealAllowedParticipantIds = new List<Guid>(),
            IssuesAllowedParticipantIds = new List<Guid>()
        };
    }
}
