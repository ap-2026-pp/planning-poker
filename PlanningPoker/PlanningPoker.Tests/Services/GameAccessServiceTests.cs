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
        _gameAccessService = new GameAccessService(_participantRepository.Object, _currentUserContext.Object);
    }

    [Fact]
    public async Task EnsureCanRevealCardsAsync_WhenRevealPolicyIsEveryone_AllowsPlayer()
    {
        var participant = CreateParticipant(_gameId, _userId, ParticipantRole.Player);
        participant.Game = CreateGame(_gameId, RevealPolicy.Everyone, IssuesPolicy.MasterOnly);

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
        var participant = CreateParticipant(_gameId, _userId, ParticipantRole.Player);
        participant.Game = CreateGame(_gameId, RevealPolicy.MasterOnly, IssuesPolicy.MasterOnly);

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
        var participant = CreateParticipant(_gameId, _userId, ParticipantRole.Player);
        participant.Game = CreateGame(_gameId, RevealPolicy.MasterOnly, IssuesPolicy.Everyone);

        SetupCurrentUserIdentity(_userId);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, _userId, null))
            .ReturnsAsync(participant);

        var result = await _gameAccessService.EnsureCanManageIssuesAsync(_gameId, "manage", "issue");

        Assert.Same(participant, result);
    }

    [Fact]
    public async Task EnsureCanManageIssuesAsync_WhenParticipantIsSpectator_ThrowsForbidden()
    {
        var participant = CreateParticipant(_gameId, _userId, ParticipantRole.Spectator);
        participant.Game = CreateGame(_gameId, RevealPolicy.MasterOnly, IssuesPolicy.Everyone);

        SetupCurrentUserIdentity(_userId);
        _participantRepository
            .Setup(repository => repository.GetCurrentParticipantAsync(_gameId, _userId, null))
            .ReturnsAsync(participant);

        var act = async () => await _gameAccessService.EnsureCanManageIssuesAsync(_gameId, "manage", "issue");

        await Assert.ThrowsAsync<ForbiddenException>(act);
    }

    [Fact]
    public async Task GetRequiredNonSpectatorParticipantAsync_WhenParticipantBelongsToAnotherGame_ThrowsNotFound()
    {
        var otherGameParticipant = CreateParticipant(Guid.NewGuid(), Guid.NewGuid(), ParticipantRole.Player);

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

    private void SetupCurrentUserIdentity(Guid userId)
    {
        _currentUserContext
            .Setup(context => context.GetCurrentParticipantIdentity())
            .Returns(new CurrentParticipantIdentity(userId, null));
    }

    private static GameParticipant CreateParticipant(Guid gameId, Guid userId, ParticipantRole role)
    {
        return new GameParticipant
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
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
            CreatedBy = Guid.NewGuid()
        };
    }
}