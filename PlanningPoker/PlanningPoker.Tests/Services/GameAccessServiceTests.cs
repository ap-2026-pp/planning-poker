using Microsoft.AspNetCore.Identity;
using Moq;
using PlanningPoker.BLL.Services;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Tests.Services;

public class GameAccessServiceTests
{
    private readonly Mock<IParticipantRepository> _participantRepository = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUserContext> _currentUserContext = new(MockBehavior.Strict);
    private readonly IGameAccessService _service;

    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GameAccessServiceTests()
    {
        _service = new GameAccessService(
            _participantRepository.Object,
            _currentUserContext.Object);
    }

    /// Активний учасник гри отримує доступ
    [Fact]
    public async Task GetRequiredParticipant_ActiveUser_ReturnsParticipant()
    {
        var participant = CreateParticipant(ParticipantRole.Player);

        SetupUser();
        SetupParticipant(participant);

        var result = await _service.GetRequiredParticipantAsync(_gameId);

        Assert.Equal(participant.Id, result.Id);

        _participantRepository.Verify(x =>
            x.GetByUserIdAndGameIdAsync(_userId, _gameId), Times.Once);

        _participantRepository.VerifyNoOtherCalls();
    }

    /// Користувача немає в грі
    [Fact]
    public async Task GetRequiredParticipant_UserNotInGame_ThrowsForbidden()
    {
        SetupUser();
        SetupMissing();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetRequiredParticipantAsync(_gameId));

        _participantRepository.Verify(x =>
            x.GetByUserIdAndGameIdAsync(_userId, _gameId), Times.Once);
    }

    /// Сценарій: Видалений учасник не має доступу
    [Fact]
    public async Task GetRequiredParticipant_RemovedUser_ThrowsForbidden()
    {
        var participant = CreateParticipant(ParticipantRole.Player);
        participant.RemovedAt = DateTime.UtcNow;

        SetupUser();
        SetupParticipant(participant);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetRequiredParticipantAsync(_gameId));
    }

    /// Сценарій: Майстер гри отримує доступ як master
    [Fact]
    public async Task GetRequiredMaster_Master_ReturnsParticipant()
    {
        var master = CreateParticipant(ParticipantRole.Master);

        SetupUser();
        SetupParticipant(master);

        var result = await _service.GetRequiredMasterAsync(_gameId);

        Assert.Equal(ParticipantRole.Master, result.Role);
    }

    /// Сценарій: Гравець не має прав майстра
    [Fact]
    public async Task GetRequiredMaster_Player_ThrowsForbidden()
    {
        SetupUser();
        SetupParticipant(CreateParticipant(ParticipantRole.Player));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetRequiredMasterAsync(_gameId));
    }

    /// Сценарій: Глядач не має прав майстра
    [Fact]
    public async Task GetRequiredMaster_Spectator_ThrowsForbidden()
    {
        SetupUser();
        SetupParticipant(CreateParticipant(ParticipantRole.Spectator));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetRequiredMasterAsync(_gameId));
    }

    /// Сценарій: Гравець може голосувати
    [Fact]
    public async Task EnsureCanVote_Player_Allows()
    {
        SetupUser();
        SetupParticipant(CreateParticipant(ParticipantRole.Player));

        var result = await _service.EnsureCanVoteAsync(_gameId);

        Assert.Equal(ParticipantRole.Player, result.Role);
    }

    /// Сценарій: Глядач не може голосувати
    [Fact]
    public async Task EnsureCanVote_Spectator_ThrowsForbidden()
    {
        SetupUser();
        SetupParticipant(CreateParticipant(ParticipantRole.Spectator));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.EnsureCanVoteAsync(_gameId));
    }

    /// Сценарій: Майстер може відкривати карти
   [Fact]
    public async Task EnsureCanRevealCards_Master_Allows()
    {
        SetupUser();

        SetupParticipant(CreateParticipant(
            ParticipantRole.Master,
            game: CreateGame(revealPolicy: RevealPolicy.MasterOnly)));

        var result = await _service.EnsureCanRevealCardsAsync(_gameId);

        Assert.Equal(ParticipantRole.Master, result.Role);
    }

    /// Сценарій: гравець не може відкривати карти
    [Fact]
    public async Task EnsureCanRevealCards_Player_ThrowsForbidden()
    {
        SetupUser();

        SetupParticipant(CreateParticipant(
            ParticipantRole.Player,
            game: CreateGame(revealPolicy: RevealPolicy.MasterOnly)));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.EnsureCanRevealCardsAsync(_gameId));
    }

    /// Перевіряє, що Master має право керувати задачами, якщо політика гри = MasterOnly.
    [Fact]
    public async Task EnsureCanManageIssues_MasterOnly_Master_Allows()
    {
        var participant = CreateParticipant(ParticipantRole.Master);
        participant.Game = CreateGame(issuesPolicy: IssuesPolicy.MasterOnly);

        SetupUser();
        SetupParticipant(participant);

        var result = await _service.EnsureCanManageIssuesAsync(_gameId);

        Assert.Equal(ParticipantRole.Master, result.Role);
    }

    /// Перевіряє, що Player отримує заборону на керування задачами при політиці MasterOnly.
    [Fact]
    public async Task EnsureCanManageIssues_MasterOnly_Player_ThrowsForbidden()
    {
        var participant = CreateParticipant(ParticipantRole.Player);
        participant.Game = CreateGame(issuesPolicy: IssuesPolicy.MasterOnly);

        SetupUser();
        SetupParticipant(participant);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.EnsureCanManageIssuesAsync(_gameId));
    }

    /// Перевіряє, що Player може керувати задачами, якщо політика дозволяє всім.
    [Fact]
    public async Task EnsureCanManageIssues_Everyone_Player_Allows()
    {
        var participant = CreateParticipant(ParticipantRole.Player);
        participant.Game = CreateGame(issuesPolicy: IssuesPolicy.Everyone);

        SetupUser();
        SetupParticipant(participant);

        var result = await _service.EnsureCanManageIssuesAsync(_gameId);

        Assert.Equal(ParticipantRole.Player, result.Role);
    }

    private void SetupUser()
    {
        _currentUserContext.Setup(x => x.GetRequiredUserId())
            .Returns(_userId);
    }

    private void SetupParticipant(GameParticipant participant)
    {
        _participantRepository.Setup(x =>
            x.GetByUserIdAndGameIdAsync(_userId, _gameId))
            .ReturnsAsync(participant);
    }

    private void SetupMissing()
    {
        _participantRepository.Setup(x =>
            x.GetByUserIdAndGameIdAsync(_userId, _gameId))
            .ReturnsAsync((GameParticipant?)null);
    }

    private GameParticipant CreateParticipant(
        ParticipantRole role,
        Guid? userId = null,
        Guid? gameId = null,
        Game? game = null)
    {
        var participantGameId = gameId ?? _gameId;

        return new GameParticipant
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? _userId,
            GameId = participantGameId,
            Game = game,
            Role = role,
            DisplayName = "test",
            IsConnected = true,
            JoinedAt = DateTime.UtcNow
        };
    }

    
    private Game CreateGame(
    Guid? id = null,
    IssuesPolicy issuesPolicy = IssuesPolicy.Everyone,
    RevealPolicy revealPolicy = RevealPolicy.MasterOnly,
    bool isActive = true)
    {
        return new Game
        {
            Id = id ?? _gameId,
            Name = "Test Game",
            InviteCode = "TEST-CODE-123",
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,

            Participants = new List<GameParticipant>(),

            IssuesPolicy = issuesPolicy,
            RevealPolicy = revealPolicy 
        };
    }
}