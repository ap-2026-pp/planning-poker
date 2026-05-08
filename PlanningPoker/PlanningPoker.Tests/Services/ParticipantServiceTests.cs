using Moq;
using PlanningPoker.BLL.Services;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.DTOs.Participant;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Tests.Services;

public class ParticipantServiceTests
{
    private readonly Mock<IParticipantRepository> _participantRepository = new();
    private readonly Mock<IGameRepository> _gameRepository = new();
    private readonly Mock<ICurrentUserContext> _currentUserContext = new();
    private readonly Mock<IGuestSessionService> _guestSessionService = new();
    private readonly Mock<IGameRoomNotifier> _gameRoomNotifier = new();
    private readonly IParticipantService _participantService;
    private readonly Guid _masterId = Guid.NewGuid();
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public ParticipantServiceTests()
    {
        var gameAccessService = new GameAccessService(_participantRepository.Object, _currentUserContext.Object);
        _participantService = new ParticipantService(
            _participantRepository.Object,
            _gameRepository.Object,
            _currentUserContext.Object,
            _guestSessionService.Object,
            gameAccessService,
            _gameRoomNotifier.Object);
    }

    [Fact]
    public async Task GetGameParticipantsAsync_WhenParticipantsExist_ReturnsMappedParticipants()
    {
        var participants = new List<GameParticipant>
        {
            CreateParticipant(_masterId, _gameId, ParticipantRole.Master, "Master"),
            CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player")
        };
    
        _currentUserContext.Setup(context => context.GetCurrentParticipantIdentity())
            .Returns(new CurrentParticipantIdentity(_playerId, null));
        _currentUserContext.Setup(context => context.GetRequiredUserId()).Returns(_playerId);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, _playerId, null))
            .ReturnsAsync(participants[1]);

        _participantRepository
            .Setup(repository => repository.GetGameParticipantsAsync(_gameId))
            .ReturnsAsync(participants);

        var result = (await _participantService.GetGameParticipantsAsync(_gameId)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("Master", result[0].DisplayName);
        Assert.Equal("Player", result[1].DisplayName);
    }

    [Fact]
    public async Task JoinGameByInviteCodeAsync_WhenAuthenticatedAndDisplayNameIsProvided_UsesRequestValue()
    {
        const string inviteCode = "invite-code";
        const string displayName = "New Player";
        var game = CreateGame(inviteCode, isActive: true);
        var currentUser = CreateUser(_playerId, "Default From Db");

        SetupAuthenticatedIdentity(currentUser);
        _gameRepository.Setup(repository => repository.GetByInviteCodeAsync(inviteCode)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, _playerId, null))
            .ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantIncludingRemovedAsync(game.Id, _playerId, null))
            .ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.ExistsByDisplayNameAsync(displayName, game.Id))
            .ReturnsAsync(false);
        _participantRepository
            .Setup(repository => repository.AddAsync(It.IsAny<GameParticipant>()))
            .Callback<GameParticipant>(participant => game.Participants.Add(participant))
            .Returns(Task.CompletedTask);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);

        var result = await _participantService.JoinGameByInviteCodeAsync(inviteCode, CreateJoinRequest(displayName));

        Assert.Equal(game.Id, result.Game.Id);
        Assert.Null(result.GuestAccessToken);
        Assert.Equal(displayName, game.Participants.Single().DisplayName);
        Assert.Equal(_playerId, game.Participants.Single().UserId);
        _guestSessionService.Verify(service => service.GenerateGuestAccessToken(It.IsAny<Guid>()), Times.Never);
        _gameRoomNotifier.Verify(
            notifier => notifier.NotifyParticipantJoinedAsync(
                game.Id,
                It.Is<GameParticipantDto>(participant => participant.DisplayName == displayName)),
            Times.Once);
    }

    [Fact]
    public async Task JoinGameByInviteCodeAsync_WhenGuestWithoutExistingParticipant_CreatesGuestParticipantAndReturnsToken()
    {
        const string inviteCode = "invite-code";
        const string displayName = "Guest Player";
        const string guestAccessToken = "guest-access-token";
        var game = CreateGame(inviteCode, isActive: true);

        SetupAnonymousGuestIdentity();
        _gameRepository.Setup(repository => repository.GetByInviteCodeAsync(inviteCode)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, null, null))
            .ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantIncludingRemovedAsync(game.Id, null, null))
            .ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.ExistsByDisplayNameAsync(displayName, game.Id))
            .ReturnsAsync(false);
        _participantRepository
            .Setup(repository => repository.AddAsync(It.IsAny<GameParticipant>()))
            .Callback<GameParticipant>(participant => game.Participants.Add(participant))
            .Returns(Task.CompletedTask);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);
        _guestSessionService
            .Setup(service => service.GenerateGuestAccessToken(It.IsAny<Guid>()))
            .Returns(guestAccessToken);

        var result = await _participantService.JoinGameByInviteCodeAsync(inviteCode, CreateJoinRequest(displayName));

        var participant = Assert.Single(game.Participants);
        Assert.Equal(game.Id, result.Game.Id);
        Assert.Equal(participant.Id, result.CurrentParticipantId);
        Assert.Equal(guestAccessToken, result.GuestAccessToken);
        Assert.Null(participant.UserId);
        Assert.Equal(displayName, participant.DisplayName);
        _guestSessionService.Verify(service => service.RevokeGuestSessionsAsync(participant.Id), Times.Once);
        _guestSessionService.Verify(service => service.CreateGuestSession(participant.Id, guestAccessToken), Times.Once);
    }

    [Fact]
    public async Task JoinGameByInviteCodeAsync_WhenGuestNameIsMissing_GeneratesFallbackDisplayName()
    {
        const string inviteCode = "invite-code";
        const string guestAccessToken = "guest-access-token";
        var game = CreateGame(inviteCode, isActive: true);

        SetupAnonymousGuestIdentity();
        _gameRepository.Setup(repository => repository.GetByInviteCodeAsync(inviteCode)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, null, null))
            .ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantIncludingRemovedAsync(game.Id, null, null))
            .ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.ExistsByDisplayNameAsync(It.Is<string>(name => name.StartsWith("Guest-")), game.Id))
            .ReturnsAsync(false);
        _participantRepository
            .Setup(repository => repository.AddAsync(It.IsAny<GameParticipant>()))
            .Callback<GameParticipant>(participant => game.Participants.Add(participant))
            .Returns(Task.CompletedTask);
        _gameRepository.Setup(repository => repository.GetByIdAsync(game.Id)).ReturnsAsync(game);
        _guestSessionService
            .Setup(service => service.GenerateGuestAccessToken(It.IsAny<Guid>()))
            .Returns(guestAccessToken);

        var result = await _participantService.JoinGameByInviteCodeAsync(inviteCode, CreateJoinRequest());

        var participant = Assert.Single(game.Participants);
        Assert.StartsWith("Guest-", participant.DisplayName);
        Assert.Equal(participant.Id, result.CurrentParticipantId);
    }

    [Fact]
    public async Task JoinGameByInviteCodeAsync_WhenGuestWasRemovedMasterInInactiveGame_ReactivatesGame()
    {
        const string inviteCode = "invite-code";
        const string guestAccessToken = "guest-access-token";
        var guestParticipantId = Guid.NewGuid();
        var game = CreateGame(inviteCode, isActive: false);
        var removedMaster = CreateGuestParticipant(game.Id, ParticipantRole.Master, "Guest Master", guestParticipantId);
        removedMaster.RemovedAt = DateTime.UtcNow.AddMinutes(-5);
        removedMaster.IsConnected = false;

        SetupGuestIdentity(guestParticipantId);
        _gameRepository.Setup(repository => repository.GetByInviteCodeAsync(inviteCode)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, null, guestParticipantId))
            .ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantIncludingRemovedAsync(game.Id, null, guestParticipantId))
            .ReturnsAsync(removedMaster);
        _participantRepository
            .Setup(repository => repository.ExistsByDisplayNameAsync("Guest Master", game.Id))
            .ReturnsAsync(false);
        _participantRepository
            .Setup(repository => repository.SaveChangesAsync())
            .Callback(() => game.Participants.Add(removedMaster))
            .Returns(Task.CompletedTask);
        _gameRepository
            .Setup(repository => repository.GetByIdAsync(game.Id))
            .ReturnsAsync(CreateGame(inviteCode, isActive: true, gameId: game.Id, participants: [removedMaster]));
        _guestSessionService
            .Setup(service => service.GenerateGuestAccessToken(guestParticipantId))
            .Returns(guestAccessToken);

        var result = await _participantService.JoinGameByInviteCodeAsync(inviteCode, CreateJoinRequest());

        Assert.True(game.IsActive);
        Assert.Equal(guestParticipantId, result.CurrentParticipantId);
        Assert.Equal(guestAccessToken, result.GuestAccessToken);
        Assert.Null(removedMaster.RemovedAt);
        Assert.True(removedMaster.IsConnected);
        _gameRepository.Verify(repository => repository.Update(game), Times.Once);
        _participantRepository.Verify(repository => repository.Update(removedMaster), Times.Once);
    }

    [Fact]
    public async Task JoinGameByInviteCodeAsync_WhenRestoredParticipantDisplayNameIsTaken_ThrowsResourceAlreadyExistsException()
    {
        const string inviteCode = "invite-code";
        var guestParticipantId = Guid.NewGuid();
        var game = CreateGame(inviteCode, isActive: true);
        var removedParticipant = CreateGuestParticipant(game.Id, ParticipantRole.Player, "Taken Name", guestParticipantId);
        removedParticipant.RemovedAt = DateTime.UtcNow.AddMinutes(-5);
        removedParticipant.IsConnected = false;

        SetupGuestIdentity(guestParticipantId);
        _gameRepository.Setup(repository => repository.GetByInviteCodeAsync(inviteCode)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, null, guestParticipantId))
            .ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantIncludingRemovedAsync(game.Id, null, guestParticipantId))
            .ReturnsAsync(removedParticipant);
        _participantRepository
            .Setup(repository => repository.ExistsByDisplayNameAsync("Taken Name", game.Id))
            .ReturnsAsync(true);

        var act = async () => await _participantService.JoinGameByInviteCodeAsync(inviteCode, CreateJoinRequest());

        await Assert.ThrowsAsync<ResourceAlreadyExistsException>(act);
    }

    [Fact]
    public async Task JoinGameByInviteCodeAsync_WhenDisplayNameAlreadyExists_ThrowsResourceAlreadyExistsException()
    {
        const string inviteCode = "invite-code";
        const string displayName = "Taken Name";
        var game = CreateGame(inviteCode, isActive: true);
        var currentUser = CreateUser(_playerId, "Default From Db");

        SetupAuthenticatedIdentity(currentUser);
        _gameRepository.Setup(repository => repository.GetByInviteCodeAsync(inviteCode)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(game.Id, _playerId, null))
            .ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantIncludingRemovedAsync(game.Id, _playerId, null))
            .ReturnsAsync((GameParticipant?)null);
        _participantRepository
            .Setup(repository => repository.ExistsByDisplayNameAsync(displayName, game.Id))
            .ReturnsAsync(true);

        var act = async () => await _participantService.JoinGameByInviteCodeAsync(inviteCode, CreateJoinRequest(displayName));

        await Assert.ThrowsAsync<ResourceAlreadyExistsException>(act);
        _participantRepository.Verify(repository => repository.AddAsync(It.IsAny<GameParticipant>()), Times.Never);
    }

    [Fact]
    public async Task LeaveGameAsync_WhenCurrentGuestIsMaster_TransfersMasterToRemainingPlayer()
    {
        var guestMasterId = Guid.NewGuid();
        var masterParticipant = CreateGuestParticipant(_gameId, ParticipantRole.Master, "Guest Master", guestMasterId);
        var playerParticipant = CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player");
        var game = CreateGame("invite-code", isActive: true, gameId: _gameId, participants: [masterParticipant, playerParticipant]);

        SetupGuestIdentity(guestMasterId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, null, guestMasterId))
            .ReturnsAsync(masterParticipant);

        await _participantService.LeaveGameAsync(_gameId);

        Assert.True(game.IsActive);
        Assert.Equal(ParticipantRole.Master, playerParticipant.Role);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(masterParticipant), Times.Once);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(playerParticipant), Times.Never);
        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
    }

    [Fact]
    public async Task LeaveGameAsync_WhenCurrentGuestIsPlayer_RemovesParticipant()
    {
        var guestPlayerId = Guid.NewGuid();
        var playerParticipant = CreateGuestParticipant(_gameId, ParticipantRole.Player, "Guest Player", guestPlayerId);
        var game = CreateGame("invite-code", isActive: true, gameId: _gameId, participants: [playerParticipant]);

        SetupGuestIdentity(guestPlayerId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, null, guestPlayerId))
            .ReturnsAsync(playerParticipant);

        await _participantService.LeaveGameAsync(_gameId);

        _participantRepository.Verify(repository => repository.RemoveGameParticipant(playerParticipant), Times.Once);
        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
        _gameRoomNotifier.Verify(
            notifier => notifier.NotifyParticipantLeftAsync(_gameId, playerParticipant.Id),
            Times.Once);
    }

    [Fact]
    public async Task LeaveGameAsync_WhenOwnerLeavesEvenIfNotMaster_ClosesSessionForAllParticipants()
    {
        var ownerId = Guid.NewGuid();
        var ownerParticipant = CreateParticipant(ownerId, _gameId, ParticipantRole.Player, "Owner");
        var delegatedMaster = CreateParticipant(_playerId, _gameId, ParticipantRole.Master, "Delegated Master");
        var game = CreateGame(
            "invite-code",
            isActive: true,
            gameId: _gameId,
            createdBy: ownerId,
            participants: [ownerParticipant, delegatedMaster]);

        SetupAuthenticatedIdentity(CreateUser(ownerId, "Owner"));
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, ownerId, null))
            .ReturnsAsync(ownerParticipant);

        await _participantService.LeaveGameAsync(_gameId);

        Assert.False(game.IsActive);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(ownerParticipant), Times.Once);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(delegatedMaster), Times.Once);
        _gameRepository.Verify(repository => repository.Update(game), Times.Once);
    }

    [Fact]
    public async Task LeaveGameAsync_WhenDelegatedMasterLeavesAndOwnerIsActive_ReturnsMasterToOwnerAndRemovesOnlyCurrentParticipant()
    {
        var ownerId = Guid.NewGuid();
        var ownerParticipant = CreateParticipant(ownerId, _gameId, ParticipantRole.Player, "Owner");
        var delegatedMaster = CreateParticipant(_playerId, _gameId, ParticipantRole.Master, "Delegated Master");
        var game = CreateGame(
            "invite-code",
            isActive: true,
            gameId: _gameId,
            createdBy: ownerId,
            participants: [ownerParticipant, delegatedMaster]);

        SetupAuthenticatedIdentity(CreateUser(_playerId, "Delegated Master"));
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, _playerId, null))
            .ReturnsAsync(delegatedMaster);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantIncludingRemovedAsync(_gameId, ownerId, null))
            .ReturnsAsync(ownerParticipant);

        await _participantService.LeaveGameAsync(_gameId);

        Assert.True(game.IsActive);
        Assert.Equal(ParticipantRole.Master, ownerParticipant.Role);
        _participantRepository.Verify(repository => repository.Update(ownerParticipant), Times.Once);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(delegatedMaster), Times.Once);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(ownerParticipant), Times.Never);
        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
    }

    [Fact]
    public async Task LeaveGameAsync_WhenDelegatedMasterLeavesAndOwnerIsInactive_TransfersMasterToAnotherActivePlayer()
    {
        var ownerId = Guid.NewGuid();
        var removedOwnerParticipant = CreateParticipant(ownerId, _gameId, ParticipantRole.Player, "Owner");
        removedOwnerParticipant.IsConnected = false;
        removedOwnerParticipant.RemovedAt = DateTime.UtcNow.AddMinutes(-5);

        var delegatedMaster = CreateParticipant(_playerId, _gameId, ParticipantRole.Master, "Delegated Master");
        var anotherPlayer = CreateGuestParticipant(_gameId, ParticipantRole.Player, "Guest Player");
        var game = CreateGame(
            "invite-code",
            isActive: true,
            gameId: _gameId,
            createdBy: ownerId,
            participants: [delegatedMaster, anotherPlayer]);

        SetupAuthenticatedIdentity(CreateUser(_playerId, "Delegated Master"));
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, _playerId, null))
            .ReturnsAsync(delegatedMaster);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantIncludingRemovedAsync(_gameId, ownerId, null))
            .ReturnsAsync(removedOwnerParticipant);

        await _participantService.LeaveGameAsync(_gameId);

        Assert.True(game.IsActive);
        Assert.Equal(ParticipantRole.Player, removedOwnerParticipant.Role);
        Assert.Equal(ParticipantRole.Master, anotherPlayer.Role);
        Assert.False(removedOwnerParticipant.IsConnected);
        Assert.NotNull(removedOwnerParticipant.RemovedAt);
        _participantRepository.Verify(repository => repository.Update(removedOwnerParticipant), Times.Never);
        _participantRepository.Verify(repository => repository.Update(anotherPlayer), Times.Once);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(delegatedMaster), Times.Once);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(anotherPlayer), Times.Never);
        _gameRepository.Verify(repository => repository.Update(It.IsAny<Game>()), Times.Never);
    }

    [Fact]
    public async Task LeaveGameAsync_WhenDelegatedMasterLeavesAndOnlySpectatorRemains_ClosesGameWithoutPromotingSpectator()
    {
        var ownerId = Guid.NewGuid();
        var spectatorOwner = CreateParticipant(ownerId, _gameId, ParticipantRole.Spectator, "Owner Spectator");
        var delegatedMaster = CreateParticipant(_playerId, _gameId, ParticipantRole.Master, "Delegated Master");
        var game = CreateGame(
            "invite-code",
            isActive: true,
            gameId: _gameId,
            createdBy: ownerId,
            participants: [spectatorOwner, delegatedMaster]);

        SetupAuthenticatedIdentity(CreateUser(_playerId, "Delegated Master"));
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, _playerId, null))
            .ReturnsAsync(delegatedMaster);
        _participantRepository
            .Setup(repository => repository.GetGameParticipantsAsync(_gameId))
            .ReturnsAsync([spectatorOwner, delegatedMaster]);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantIncludingRemovedAsync(_gameId, ownerId, null))
            .ReturnsAsync(spectatorOwner);

        await _participantService.LeaveGameAsync(_gameId);

        Assert.False(game.IsActive);
        Assert.Equal(ParticipantRole.Spectator, spectatorOwner.Role);
        _participantRepository.Verify(repository => repository.Update(spectatorOwner), Times.Never);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(delegatedMaster), Times.Once);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(spectatorOwner), Times.Once);
        _gameRepository.Verify(repository => repository.Update(game), Times.Once);
    }

    [Fact]
    public async Task DeleteGameParticipantAsync_WhenCurrentGuestIsMaster_RemovesParticipant()
    {
        var guestMasterId = Guid.NewGuid();
        var masterParticipant = CreateGuestParticipant(_gameId, ParticipantRole.Master, "Guest Master", guestMasterId);
        var playerParticipant = CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player");

        SetupGuestIdentity(guestMasterId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(CreateGame("invite-code", true, _gameId));
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, null, guestMasterId))
            .ReturnsAsync(masterParticipant);
        _participantRepository
            .Setup(repository => repository.GetActiveByIdAsync(playerParticipant.Id))
            .ReturnsAsync(playerParticipant);

        await _participantService.DeleteGameParticipantAsync(_gameId, playerParticipant.Id);

        _participantRepository.Verify(repository => repository.RemoveGameParticipant(playerParticipant), Times.Once);
        _gameRoomNotifier.Verify(
            notifier => notifier.NotifyParticipantKickedAsync(_gameId, playerParticipant.Id),
            Times.Once);
    }

    [Fact]
    public async Task DeleteGameParticipantAsync_WhenGameDoesNotExist_ThrowsNotFoundException()
    {
        var gameId = Guid.NewGuid();

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(gameId))
            .ReturnsAsync((Game?)null);

        var act = async () => await _participantService.DeleteGameParticipantAsync(gameId, Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task UpdateDisplayNameAsync_WhenCurrentParticipantIsGuest_UpdatesParticipant()
    {
        var guestParticipantId = Guid.NewGuid();
        const string newDisplayName = "Updated Guest";
        var participant = CreateGuestParticipant(_gameId, ParticipantRole.Player, "Guest", guestParticipantId);
        var game = CreateGame("invite-code", true, _gameId);

        SetupGuestIdentity(guestParticipantId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, null, guestParticipantId))
            .ReturnsAsync(participant);
        _participantRepository
            .Setup(repository => repository.ExistsByDisplayNameAsync(newDisplayName, _gameId))
            .ReturnsAsync(false);

        var result = await _participantService.UpdateDisplayNameAsync(_gameId, newDisplayName);

        Assert.Equal(newDisplayName, result.DisplayName);
        Assert.Equal(newDisplayName, participant.DisplayName);
        _gameRoomNotifier.Verify(
            notifier => notifier.NotifyMasterChangedAsync(
                _gameId,
                It.Is<GameParticipantDto>(updatedParticipant =>
                    updatedParticipant.Id == participant.Id &&
                    updatedParticipant.DisplayName == newDisplayName)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateDisplayNameAsync_WhenAuthenticatedDisplayNameIsMissing_UsesCurrentUserDisplayName()
    {
        var currentUser = CreateUser(_playerId, "Default From Db");
        var participant = CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player");
        var game = CreateGame("invite-code", true, _gameId);

        SetupAuthenticatedIdentity(currentUser);
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, _playerId, null))
            .ReturnsAsync(participant);
        _participantRepository
            .Setup(repository => repository.ExistsByDisplayNameAsync(currentUser.DisplayName, _gameId))
            .ReturnsAsync(false);

        var result = await _participantService.UpdateDisplayNameAsync(_gameId, null);

        Assert.Equal(currentUser.DisplayName, result.DisplayName);
        Assert.Equal(currentUser.DisplayName, participant.DisplayName);
    }

    [Fact]
    public async Task TransferMasterAsync_WhenCurrentGuestIsMaster_TransfersRights()
    {
        var guestMasterId = Guid.NewGuid();
        var masterParticipant = CreateGuestParticipant(_gameId, ParticipantRole.Master, "Guest Master", guestMasterId);
        var playerParticipant = CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player");

        SetupGuestIdentity(guestMasterId);
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(CreateGame("invite-code", true, _gameId));
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, null, guestMasterId))
            .ReturnsAsync(masterParticipant);
        _participantRepository
            .Setup(repository => repository.GetActiveByIdAsync(playerParticipant.Id))
            .ReturnsAsync(playerParticipant);

        await _participantService.TransferMasterAsync(_gameId, playerParticipant.Id);

        Assert.Equal(ParticipantRole.Player, masterParticipant.Role);
        Assert.Equal(ParticipantRole.Master, playerParticipant.Role);
        _gameRoomNotifier.Verify(
            notifier => notifier.NotifyMasterChangedAsync(
                _gameId,
                It.Is<GameParticipantDto>(updatedParticipant =>
                    updatedParticipant.Id == masterParticipant.Id &&
                    updatedParticipant.Role == ParticipantRole.Player)),
            Times.Once);
        _gameRoomNotifier.Verify(
            notifier => notifier.NotifyMasterChangedAsync(
                _gameId,
                It.Is<GameParticipantDto>(updatedParticipant =>
                    updatedParticipant.Id == playerParticipant.Id &&
                    updatedParticipant.Role == ParticipantRole.Master)),
            Times.Once);
    }

    [Fact]
    public async Task SetSpectatorModeAsync_WhenPlayerSwitchesToSpectator_NotifiesRoom()
    {
        var participant = CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player");
        var game = CreateGame("invite-code", true, _gameId);

        SetupAuthenticatedIdentity(CreateUser(_playerId, "Player"));
        _gameRepository.Setup(repository => repository.GetByIdAsync(_gameId)).ReturnsAsync(game);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, _playerId, null))
            .ReturnsAsync(participant);

        await _participantService.SetSpectatorModeAsync(_gameId, true);

        Assert.Equal(ParticipantRole.Spectator, participant.Role);
        _gameRoomNotifier.Verify(
            notifier => notifier.NotifyMasterChangedAsync(
                _gameId,
                It.Is<GameParticipantDto>(updatedParticipant =>
                    updatedParticipant.Id == participant.Id &&
                    updatedParticipant.Role == ParticipantRole.Spectator)),
            Times.Once);
    }

    [Fact]
    public async Task TransferMasterAsync_WhenGameDoesNotExist_ThrowsNotFoundException()
    {
        var gameId = Guid.NewGuid();

        _gameRepository
            .Setup(repository => repository.GetByIdAsync(gameId))
            .ReturnsAsync((Game?)null);

        var act = async () => await _participantService.TransferMasterAsync(gameId, Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    private void SetupAuthenticatedIdentity(User user)
    {
        _currentUserContext.Setup(context => context.GetCurrentParticipantIdentity())
            .Returns(new CurrentParticipantIdentity(user.Id, null));
        _currentUserContext.Setup(context => context.GetUserOrDefaultAsync()).ReturnsAsync(user);
        _currentUserContext.Setup(context => context.GetRequiredUserAsync()).ReturnsAsync(user);
        _currentUserContext.Setup(context => context.GetRequiredUserId()).Returns(user.Id);
    }

    private void SetupAnonymousGuestIdentity()
    {
        _currentUserContext.Setup(context => context.GetCurrentParticipantIdentity())
            .Returns(new CurrentParticipantIdentity(null, null));
        _currentUserContext.Setup(context => context.GetUserOrDefaultAsync()).ReturnsAsync((User?)null);
    }

    private void SetupGuestIdentity(Guid participantId)
    {
        _currentUserContext.Setup(context => context.GetCurrentParticipantIdentity())
            .Returns(new CurrentParticipantIdentity(null, participantId));
        _currentUserContext.Setup(context => context.GetUserOrDefaultAsync()).ReturnsAsync((User?)null);
    }

    private static JoinGameRequestDto CreateJoinRequest(string? displayName = null, ParticipantRole? role = null)
    {
        return new JoinGameRequestDto
        {
            DisplayName = displayName,
            ParticipantRole = role
        };
    }

    private static User CreateUser(Guid userId, string displayName)
    {
        return new User
        {
            Id = userId,
            DisplayName = displayName
        };
    }

    private static Game CreateGame(
        string inviteCode,
        bool isActive,
        Guid? gameId = null,
        Guid? createdBy = null,
        IEnumerable<GameParticipant>? participants = null)
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
            CreatedBy = createdBy ?? Guid.Empty,
            Participants = participants?.ToList() ?? []
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

    private static GameParticipant CreateGuestParticipant(
        Guid gameId,
        ParticipantRole role,
        string displayName,
        Guid? participantId = null)
    {
        return new GameParticipant
        {
            Id = participantId ?? Guid.NewGuid(),
            UserId = null,
            GameId = gameId,
            Role = role,
            DisplayName = displayName,
            IsConnected = true,
            JoinedAt = DateTime.UtcNow
        };
    }
}
