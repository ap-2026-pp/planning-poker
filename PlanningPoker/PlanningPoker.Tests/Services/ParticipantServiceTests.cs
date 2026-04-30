using Moq;
using PlanningPoker.BLL.Services;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Tests.Services;

public class ParticipantServiceTests
{
    private readonly Mock<IParticipantRepository> _participantRepository = new();
    private readonly Mock<IGameRepository> _gameRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly IParticipantService _participantService;
    private readonly Guid _masterId = Guid.NewGuid();
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public ParticipantServiceTests()
    {
        _participantService = new ParticipantService(
            _participantRepository.Object,
            _gameRepository.Object,
            _userRepository.Object);
    }

    [Fact]
    public async Task GetGameParticipantsAsync_WhenParticipantsExist_ReturnsMappedParticipants()
    {
        var participants = new List<GameParticipant>
        {
            CreateParticipant(_masterId, _gameId, ParticipantRole.Master, "Master"),
            CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player")
        };

        _participantRepository
            .Setup(repository => repository.GetGameParticipantsAsync(_gameId))
            .ReturnsAsync(participants);

        var result = (await _participantService.GetGameParticipantsAsync(_gameId)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(participants[0].Id, result[0].Id);
        Assert.Equal(participants[0].DisplayName, result[0].DisplayName);
        Assert.Equal(participants[0].Role, result[0].Role);
        Assert.Equal(participants[1].Id, result[1].Id);
        Assert.Equal(participants[1].DisplayName, result[1].DisplayName);
        Assert.Equal(participants[1].Role, result[1].Role);
    }

    [Fact]
    public async Task JoinGameByInviteCodeAsync_WhenDisplayNameIsProvided_UsesRequestValue()
    {
        const string inviteCode = "invite-code";
        const string displayName = "New Player";
        var game = CreateGame(inviteCode, isActive: true);

        SetupCurrentUser(CreateUser(_playerId, "Default From Db"));
        _gameRepository.Setup(repository => repository.GetByInviteCodeAsync(inviteCode)).ReturnsAsync(game);
        _participantRepository.Setup(repository => repository.ExistsByDisplayNameAsync(displayName, game.Id)).ReturnsAsync(false);
        _participantRepository.Setup(repository => repository.GetByUserIdAndGameIdAsync(_playerId, game.Id)).ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.GetByUserIdAndGameIdIncludingRemovedAsync(_playerId, game.Id))
            .ReturnsAsync((GameParticipant?)null);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);

        var result = await _participantService.JoinGameByInviteCodeAsync(_playerId, inviteCode, displayName);

        Assert.Equal(game.Id, result.Id);
        _participantRepository.Verify(repository => repository.AddAsync(It.Is<GameParticipant>(participant =>
            participant.GameId == game.Id &&
            participant.UserId == _playerId &&
            participant.DisplayName == displayName &&
            participant.Role == ParticipantRole.Player &&
            participant.IsConnected)), Times.Once);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task JoinGameByInviteCodeAsync_WhenDisplayNameIsMissing_UsesCurrentUserDisplayNameFromDb()
    {
        const string inviteCode = "invite-code";
        var game = CreateGame(inviteCode, isActive: true);

        SetupCurrentUser(CreateUser(_playerId, "Default From Db"));
        _gameRepository.Setup(repository => repository.GetByInviteCodeAsync(inviteCode)).ReturnsAsync(game);
        _participantRepository.Setup(repository => repository.ExistsByDisplayNameAsync("Default From Db", game.Id)).ReturnsAsync(false);
        _participantRepository.Setup(repository => repository.GetByUserIdAndGameIdAsync(_playerId, game.Id)).ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.GetByUserIdAndGameIdIncludingRemovedAsync(_playerId, game.Id))
            .ReturnsAsync((GameParticipant?)null);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);

        await _participantService.JoinGameByInviteCodeAsync(_playerId, inviteCode, null);

        _participantRepository.Verify(repository => repository.AddAsync(It.Is<GameParticipant>(participant =>
            participant.DisplayName == "Default From Db")), Times.Once);
    }

    [Fact]
    public async Task JoinGameByInviteCodeAsync_WhenUserWasRemoved_ReconnectsExistingParticipant()
    {
        const string inviteCode = "invite-code";
        const string displayName = "Returning Player";
        var game = CreateGame(inviteCode, isActive: true);
        var removedParticipant = CreateParticipant(_playerId, game.Id, ParticipantRole.Player, displayName);
        removedParticipant.RemovedAt = DateTime.UtcNow.AddMinutes(-5);
        removedParticipant.IsConnected = false;

        SetupCurrentUser(CreateUser(_playerId, "Default From Db"));
        _gameRepository.Setup(repository => repository.GetByInviteCodeAsync(inviteCode)).ReturnsAsync(game);
        _participantRepository.Setup(repository => repository.ExistsByDisplayNameAsync(displayName, game.Id)).ReturnsAsync(false);
        _participantRepository.Setup(repository => repository.GetByUserIdAndGameIdAsync(_playerId, game.Id)).ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.GetByUserIdAndGameIdIncludingRemovedAsync(_playerId, game.Id))
            .ReturnsAsync(removedParticipant);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);

        await _participantService.JoinGameByInviteCodeAsync(_playerId, inviteCode, displayName);

        Assert.True(removedParticipant.IsConnected);
        Assert.Null(removedParticipant.RemovedAt);
        _participantRepository.Verify(repository => repository.Update(removedParticipant), Times.Once);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task JoinGameByInviteCodeAsync_WhenDisplayNameAlreadyExists_ThrowsResourceAlreadyExistsException()
    {
        const string inviteCode = "invite-code";
        const string displayName = "Taken Name";
        var game = CreateGame(inviteCode, isActive: true);

        SetupCurrentUser(CreateUser(_playerId, "Default From Db"));
        _gameRepository.Setup(repository => repository.GetByInviteCodeAsync(inviteCode)).ReturnsAsync(game);
        _participantRepository.Setup(repository => repository.ExistsByDisplayNameAsync(displayName, game.Id)).ReturnsAsync(true);

        var act = async () => await _participantService.JoinGameByInviteCodeAsync(_playerId, inviteCode, displayName);

        await Assert.ThrowsAsync<ResourceAlreadyExistsException>(act);
        _participantRepository.Verify(repository => repository.AddAsync(It.IsAny<GameParticipant>()), Times.Never);
    }

    [Fact]
    public async Task JoinGameByInviteCodeAsync_WhenInactiveGameAndRemovedMaster_ReactivatesGameAndRestoresParticipant()
    {
        const string inviteCode = "invite-code";
        var game = CreateGame(inviteCode, isActive: false);
        var removedMaster = CreateParticipant(_masterId, game.Id, ParticipantRole.Master, "Master");
        removedMaster.RemovedAt = DateTime.UtcNow.AddMinutes(-5);
        removedMaster.IsConnected = false;

        SetupCurrentUser(CreateUser(_masterId, "Master"));
        _gameRepository.Setup(repository => repository.GetByInviteCodeAsync(inviteCode)).ReturnsAsync(game);
        _participantRepository.Setup(repository => repository.GetByUserIdAndGameIdAsync(_masterId, game.Id)).ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.GetByUserIdAndGameIdIncludingRemovedAsync(_masterId, game.Id))
            .ReturnsAsync(removedMaster);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);

        var result = await _participantService.JoinGameByInviteCodeAsync(_masterId, inviteCode, "Master");

        Assert.Equal(game.Id, result.Id);
        Assert.True(game.IsActive);
        Assert.True(removedMaster.IsConnected);
        Assert.Null(removedMaster.RemovedAt);
        _gameRepository.Verify(repository => repository.Update(game), Times.Once);
        _participantRepository.Verify(repository => repository.Update(removedMaster), Times.Once);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task JoinGameByInviteCodeAsync_WhenInactiveGameAndCurrentUserIsNotMaster_ThrowsForbiddenException()
    {
        const string inviteCode = "invite-code";
        var game = CreateGame(inviteCode, isActive: false);

        SetupCurrentUser(CreateUser(_playerId, "Player"));
        _gameRepository.Setup(repository => repository.GetByInviteCodeAsync(inviteCode)).ReturnsAsync(game);
        _participantRepository.Setup(repository => repository.GetByUserIdAndGameIdAsync(_playerId, game.Id)).ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.GetByUserIdAndGameIdIncludingRemovedAsync(_playerId, game.Id))
            .ReturnsAsync((GameParticipant?)null);

        var act = async () => await _participantService.JoinGameByInviteCodeAsync(_playerId, inviteCode, "Player");

        await Assert.ThrowsAsync<ForbiddenException>(act);
        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task LeaveGameAsync_WhenCurrentUserIsMaster_ClosesSessionForAllParticipants()
    {
        var masterParticipant = CreateParticipant(_masterId, _gameId, ParticipantRole.Master, "Master");
        var playerParticipant = CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player");
        var game = CreateGame("invite-code", isActive: true, gameId: _gameId);
        game.Participants.Add(masterParticipant);
        game.Participants.Add(playerParticipant);

        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);

        await _participantService.LeaveGameAsync(_gameId, _masterId);

        Assert.False(game.IsActive);
        _gameRepository.Verify(repository => repository.Update(game), Times.Once);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(masterParticipant), Times.Once);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(playerParticipant), Times.Once);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task LeaveGameAsync_WhenMasterIsTheOnlyParticipant_ClosesSessionAndRemovesMaster()
    {
        var masterParticipant = CreateParticipant(_masterId, _gameId, ParticipantRole.Master, "Master");
        var game = CreateGame("invite-code", isActive: true, gameId: _gameId);
        game.Participants.Add(masterParticipant);

        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);

        await _participantService.LeaveGameAsync(_gameId, _masterId);

        Assert.False(game.IsActive);
        _gameRepository.Verify(repository => repository.Update(game), Times.Once);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(masterParticipant), Times.Once);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task LeaveGameAsync_WhenCurrentUserIsPlayer_RemovesParticipant()
    {
        var playerParticipant = CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player");
        var game = CreateGame("invite-code", isActive: true, gameId: _gameId);
        game.Participants.Add(CreateParticipant(_masterId, _gameId, ParticipantRole.Master, "Master"));
        game.Participants.Add(playerParticipant);

        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);

        await _participantService.LeaveGameAsync(_gameId, _playerId);

        _participantRepository.Verify(repository => repository.RemoveGameParticipant(playerParticipant), Times.Once);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteGameParticipantAsync_WhenGameDoesNotExist_ThrowsNotFoundException()
    {
        var participantId = Guid.NewGuid();

        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync((Game?)null);

        var act = async () => await _participantService.DeleteGameParticipantAsync(_gameId, _masterId, participantId);

        await Assert.ThrowsAsync<NotFoundException>(act);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(It.IsAny<GameParticipant>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGameParticipantAsync_WhenCurrentUserIsNotMaster_ThrowsForbiddenException()
    {
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(CreateGame("invite-code", isActive: true));
        _participantRepository
            .Setup(repository => repository.GetByUserIdAndGameIdAsync(_playerId, _gameId))
            .ReturnsAsync(CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player"));

        var act = async () => await _participantService.DeleteGameParticipantAsync(_gameId, _playerId, Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(act);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(It.IsAny<GameParticipant>()), Times.Never);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteGameParticipantAsync_WhenCurrentUserIsMaster_RemovesParticipantAndSavesChanges()
    {
        var participantId = Guid.NewGuid();
        var participant = CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player", participantId);

        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(CreateGame("invite-code", isActive: true));
        _participantRepository
            .Setup(repository => repository.GetByUserIdAndGameIdAsync(_masterId, _gameId))
            .ReturnsAsync(CreateParticipant(_masterId, _gameId, ParticipantRole.Master, "Master"));
        _participantRepository
            .Setup(repository => repository.GetActiveByIdAsync(participantId))
            .ReturnsAsync(participant);

        await _participantService.DeleteGameParticipantAsync(_gameId, _masterId, participantId);

        _participantRepository.Verify(repository => repository.RemoveGameParticipant(participant), Times.Once);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateDisplayNameAsync_WhenDisplayNameIsAvailable_UpdatesParticipant()
    {
        const string newDisplayName = "Updated Player";
        var game = CreateGame("invite-code", isActive: true, gameId: _gameId);
        var participant = CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player");

        SetupCurrentUser(CreateUser(_playerId, "Default From Db"));
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);
        _participantRepository.Setup(repository => repository.ExistsByDisplayNameAsync(newDisplayName, _gameId)).ReturnsAsync(false);
        _participantRepository
            .Setup(repository => repository.GetByUserIdAndGameIdAsync(_playerId, _gameId))
            .ReturnsAsync(participant);

        var result = await _participantService.UpdateDisplayNameAsync(_gameId, _playerId, newDisplayName);

        Assert.Equal(newDisplayName, result.DisplayName);
        Assert.Equal(newDisplayName, participant.DisplayName);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateDisplayNameAsync_WhenDisplayNameIsMissing_UsesCurrentUserDisplayNameFromDb()
    {
        var game = CreateGame("invite-code", isActive: true, gameId: _gameId);
        var participant = CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player");

        SetupCurrentUser(CreateUser(_playerId, "Default From Db"));
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);
        _participantRepository.Setup(repository => repository.ExistsByDisplayNameAsync("Default From Db", _gameId)).ReturnsAsync(false);
        _participantRepository
            .Setup(repository => repository.GetByUserIdAndGameIdAsync(_playerId, _gameId))
            .ReturnsAsync(participant);

        var result = await _participantService.UpdateDisplayNameAsync(_gameId, _playerId, null);

        Assert.Equal("Default From Db", result.DisplayName);
        Assert.Equal("Default From Db", participant.DisplayName);
    }

    [Fact]
    public async Task UpdateDisplayNameAsync_WhenDisplayNameAlreadyExists_ThrowsResourceAlreadyExistsException()
    {
        const string newDisplayName = "Taken Name";
        var game = CreateGame("invite-code", isActive: true, gameId: _gameId);
        var participant = CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player");

        SetupCurrentUser(CreateUser(_playerId, "Default From Db"));
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);
        _participantRepository.Setup(repository => repository.ExistsByDisplayNameAsync(newDisplayName, _gameId)).ReturnsAsync(true);
        _participantRepository
            .Setup(repository => repository.GetByUserIdAndGameIdAsync(_playerId, _gameId))
            .ReturnsAsync(participant);

        var act = async () => await _participantService.UpdateDisplayNameAsync(_gameId, _playerId, newDisplayName);

        await Assert.ThrowsAsync<ResourceAlreadyExistsException>(act);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    private void SetupCurrentUser(User user)
    {
        _userRepository.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
    }

    private static User CreateUser(Guid userId, string displayName)
    {
        return new User
        {
            Id = userId,
            DisplayName = displayName
        };
    }

    private static Game CreateGame(string inviteCode, bool isActive, Guid? gameId = null)
    {
        return new Game
        {
            Id = gameId ?? Guid.NewGuid(),
            Name = "Demo Game",
            InviteCode = inviteCode,
            VotingSystem = VotingSystem.Custom,
            AutoRevealCards = true,
            ShowAverage = true,
            ShowCountdownAnimation = true,
            IsActive = isActive,
            Participants = []
        };
    }

    private static GameParticipant CreateParticipant(
        Guid userId,
        Guid gameId,
        ParticipantRole role,
        string displayName,
        Guid? participantId = null)
    {
        return new GameParticipant
        {
            Id = participantId ?? Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            Role = role,
            DisplayName = displayName,
            IsConnected = true,
            JoinedAt = DateTime.UtcNow
        };
    }
}
