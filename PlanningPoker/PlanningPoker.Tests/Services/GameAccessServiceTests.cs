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

    /// Сценарій: майстер може керувати задачами
    [Fact]
    public async Task EnsureCanManageIssues_Master_Allows()
    {
        SetupUser();
        SetupParticipant(CreateParticipant(ParticipantRole.Master));

        var result = await _service.EnsureCanManageIssuesAsync(_gameId);

        Assert.Equal(ParticipantRole.Master, result.Role);
    }

    /// Сценарій: Гравець не може керувати задачами
    [Fact]
    public async Task EnsureCanManageIssues_Player_ThrowsForbidden()
    {
        SetupUser();
        SetupParticipant(CreateParticipant(ParticipantRole.Player));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.EnsureCanManageIssuesAsync(_gameId));
    }

    /// Сценарій: Майстер може відкривати карти
    [Fact]
    public async Task EnsureCanRevealCards_Master_Allows()
    {
        SetupUser();
        SetupParticipant(CreateParticipant(ParticipantRole.Master));

        var result = await _service.EnsureCanRevealCardsAsync(_gameId);

        Assert.Equal(ParticipantRole.Master, result.Role);
    }

    /// Сценарій: гравець не може відкривати карти
    [Fact]
    public async Task EnsureCanRevealCards_Player_ThrowsForbidden()
    {
        SetupUser();
        SetupParticipant(CreateParticipant(ParticipantRole.Player));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.EnsureCanRevealCardsAsync(_gameId));
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

    private GameParticipant CreateParticipant(ParticipantRole role)
    {
        return new GameParticipant
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            GameId = _gameId,
            Role = role,
            DisplayName = "test",
            IsConnected = true,
            JoinedAt = DateTime.UtcNow
        };
    }
}