using Moq;
using PlanningPoker.BLL.Services;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Tests.Services;

public class GameAccessServiceTests
{
    private readonly Mock<IParticipantRepository> _participantRepository = new();
    private readonly Mock<ICurrentUserContext> _currentUserContext = new();
    private readonly IGameAccessService _gameAccessService;
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GameAccessServiceTests()
    {
        _gameAccessService = new GameAccessService(
            _participantRepository.Object,
            _currentUserContext.Object);
    }

    [Fact]
    public async Task EnsureCanRevealCardsAsync_WhenRevealPolicyIsEveryone_AllowsPlayer()
    {
        var game = CreateGame(_gameId, RevealPolicy.Everyone, IssuesPolicy.MasterOnly);
        var participant = CreateParticipant(_gameId, _userId, ParticipantRole.Player, game);

        SetupCurrentUserIdentity(_userId);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, _userId, null))
            .ReturnsAsync(participant);

        var result = await _gameAccessService.EnsureCanRevealCardsAsync(_gameId);

        Assert.Same(participant, result);
    }

    [Fact]
    public async Task EnsureCanRevealCardsAsync_WhenRevealPolicyIsMasterOnlyAndParticipantIsNotMaster_ThrowsForbidden()
    {
        var game = CreateGame(_gameId, RevealPolicy.MasterOnly, IssuesPolicy.MasterOnly);
        var participant = CreateParticipant(_gameId, _userId, ParticipantRole.Player, game);

        SetupCurrentUserIdentity(_userId);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, _userId, null))
            .ReturnsAsync(participant);

        var act = async () => await _gameAccessService.EnsureCanRevealCardsAsync(_gameId);

        await Assert.ThrowsAsync<ForbiddenException>(act);
    }

    [Fact]
    public async Task EnsureCanManageIssuesAsync_WhenIssuesPolicyIsEveryone_AllowsPlayer()
    {
        var game = CreateGame(_gameId, RevealPolicy.MasterOnly, IssuesPolicy.Everyone);
        var participant = CreateParticipant(_gameId, _userId, ParticipantRole.Player, game);

        SetupCurrentUserIdentity(_userId);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, _userId, null))
            .ReturnsAsync(participant);

        var result = await _gameAccessService.EnsureCanManageIssuesAsync(
            _gameId,
            "manage",
            "issue");

        Assert.Same(participant, result);
    }

    [Fact]
    public async Task EnsureCanManageIssuesAsync_WhenParticipantIsMaster_AllowsManagingIssues()
    {
        var game = CreateGame(_gameId, RevealPolicy.MasterOnly, IssuesPolicy.MasterOnly);
        var participant = CreateParticipant(_gameId, _userId, ParticipantRole.Master, game);

        participant.CanManageIssues = false;

        SetupCurrentUserIdentity(_userId);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, _userId, null))
            .ReturnsAsync(participant);

        var result = await _gameAccessService.EnsureCanManageIssuesAsync(
            _gameId,
            "manage",
            "issue");

        Assert.Same(participant, result);
    }

    [Fact]
    public async Task EnsureCanManageIssuesAsync_WhenParticipantIsSpectator_ThrowsForbidden()
    {
        var game = CreateGame(_gameId, RevealPolicy.MasterOnly, IssuesPolicy.Everyone);
        var participant = CreateParticipant(_gameId, _userId, ParticipantRole.Spectator, game);

        SetupCurrentUserIdentity(_userId);

        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, _userId, null))
            .ReturnsAsync(participant);

        var act = async () => await _gameAccessService.EnsureCanManageIssuesAsync(
            _gameId,
            "manage",
            "issue");

        await Assert.ThrowsAsync<ForbiddenException>(act);
    }

    [Fact]
    public async Task GetRequiredNonSpectatorParticipantAsync_WhenParticipantBelongsToAnotherGame_ThrowsNotFound()
    {
        var otherGameId = Guid.NewGuid();
        var otherGame = CreateGame(otherGameId, RevealPolicy.MasterOnly, IssuesPolicy.MasterOnly);
        var otherGameParticipant = CreateParticipant(
            otherGameId,
            Guid.NewGuid(),
            ParticipantRole.Player,
            otherGame);

        _participantRepository
            .Setup(repository => repository.GetActiveByIdAsync(otherGameParticipant.Id))
            .ReturnsAsync(otherGameParticipant);

        var act = async () => await _gameAccessService.GetRequiredNonSpectatorParticipantAsync(
            _gameId,
            otherGameParticipant.Id,
            "transfer master to",
            "participant");

        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task GetOtherActiveNonSpectatorParticipantsAsync_WhenRepositoryReturnsParticipants_FiltersSpectatorsAndExcludedParticipant()
    {
        var game = CreateGame(_gameId, RevealPolicy.MasterOnly, IssuesPolicy.MasterOnly);

        var excludedParticipant = CreateParticipant(_gameId, _userId, ParticipantRole.Master, game);
        var playerParticipant = CreateParticipant(_gameId, Guid.NewGuid(), ParticipantRole.Player, game);
        var spectatorParticipant = CreateParticipant(_gameId, Guid.NewGuid(), ParticipantRole.Spectator, game);
        var removedParticipant = CreateParticipant(_gameId, Guid.NewGuid(), ParticipantRole.Player, game);

        removedParticipant.RemovedAt = DateTime.UtcNow.AddMinutes(-5);

        game.Participants = new List<GameParticipant>
        {
            excludedParticipant,
            playerParticipant,
            spectatorParticipant,
            removedParticipant
        };

        _participantRepository
            .Setup(repository => repository.GetGameParticipantsAsync(_gameId))
            .ReturnsAsync(new List<GameParticipant>
            {
                excludedParticipant,
                playerParticipant,
                spectatorParticipant,
                removedParticipant
            });

        var result = await _gameAccessService.GetOtherActiveNonSpectatorParticipantsAsync(
            game,
            excludedParticipant.Id);

        var participants = result.ToList();

        Assert.Single(participants);
        Assert.Same(playerParticipant, participants[0]);
    }

    [Fact]
    public async Task GetOtherActiveNonSpectatorParticipantsAsync_WhenRepositoryReturnsEmpty_UsesGameParticipantsFallback()
    {
        var game = CreateGame(_gameId, RevealPolicy.MasterOnly, IssuesPolicy.MasterOnly);

        var excludedParticipant = CreateParticipant(_gameId, _userId, ParticipantRole.Master, game);
        var playerParticipant = CreateParticipant(_gameId, Guid.NewGuid(), ParticipantRole.Player, game);
        var spectatorParticipant = CreateParticipant(_gameId, Guid.NewGuid(), ParticipantRole.Spectator, game);

        game.Participants = new List<GameParticipant>
        {
            excludedParticipant,
            playerParticipant,
            spectatorParticipant
        };

        _participantRepository
            .Setup(repository => repository.GetGameParticipantsAsync(_gameId))
            .ReturnsAsync(new List<GameParticipant>());

        var result = await _gameAccessService.GetOtherActiveNonSpectatorParticipantsAsync(
            game,
            excludedParticipant.Id);

        var participants = result.ToList();

        Assert.Single(participants);
        Assert.Same(playerParticipant, participants[0]);
    }

    private void SetupCurrentUserIdentity(Guid userId)
    {
        _currentUserContext
            .Setup(context => context.GetCurrentParticipantIdentity())
            .Returns(new CurrentParticipantIdentity(userId, null));
    }

    private static GameParticipant CreateParticipant(
        Guid gameId,
        Guid userId,
        ParticipantRole role,
        Game? game = null)
    {
        return new GameParticipant
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Game = game ?? CreateGame(gameId, RevealPolicy.MasterOnly, IssuesPolicy.MasterOnly),
            UserId = userId,
            DisplayName = "Participant",
            Role = role,
            IsConnected = true,
            JoinedAt = DateTime.UtcNow
        };
    }

    private static Game CreateGame(Guid gameId, RevealPolicy revealPolicy, IssuesPolicy issuesPolicy)
    {
        return new Game
        {
            Id = gameId,
            Name = "Test Game",
            InviteCode = "TEST-CODE-123",
            RevealPolicy = revealPolicy,
            IssuesPolicy = issuesPolicy,
            CreatedBy = Guid.NewGuid(),
            IsActive = true,
            IsDeleted = false,
            AutoRevealCards = false,
            TimerEndsAt = null,
            Participants = new List<GameParticipant>()
        };
    }
}
