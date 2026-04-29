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
    private readonly Mock<ICurrentUserService> _currentUserService = new();
    private readonly IParticipantService _participantService;
    private readonly Guid _masterId = Guid.NewGuid();
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public ParticipantServiceTests()
    {
        _participantService = new ParticipantService(_participantRepository.Object, _currentUserService.Object);
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

        var result = (await _participantService.GetGameParticipantsAsync(_gameId))!.ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(participants[0].Id, result[0].Id);
        Assert.Equal(participants[0].DisplayName, result[0].DisplayName);
        Assert.Equal(participants[0].Role, result[0].Role);
        Assert.Equal(participants[1].Id, result[1].Id);
        Assert.Equal(participants[1].DisplayName, result[1].DisplayName);
        Assert.Equal(participants[1].Role, result[1].Role);
    }

    [Fact]
    public async Task GetGameParticipantsAsync_WhenRepositoryReturnsNull_ReturnsEmptyCollection()
    {
        _participantRepository
            .Setup(repository => repository.GetGameParticipantsAsync(_gameId))
            .ReturnsAsync((IEnumerable<GameParticipant>?)null);

        var result = await _participantService.GetGameParticipantsAsync(_gameId);

        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public async Task DeleteGameParticipantAsync_WhenCurrentUserIsNotMaster_ThrowsForbiddenException()
    {
        SetupCurrentUser(_playerId);
        _participantRepository
            .Setup(repository => repository.GetByUserIdAndGameIdAsync(_playerId, _gameId))
            .ReturnsAsync(CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player"));

        var act = async () => await _participantService.DeleteGameParticipantAsync(_gameId, Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(act);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(It.IsAny<GameParticipant>()), Times.Never);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteGameParticipantAsync_WhenParticipantDoesNotExist_ThrowsNotFoundException()
    {
        var participantId = Guid.NewGuid();

        SetupCurrentUser(_masterId);
        _participantRepository
            .Setup(repository => repository.GetByUserIdAndGameIdAsync(_masterId, _gameId))
            .ReturnsAsync(CreateParticipant(_masterId, _gameId, ParticipantRole.Master, "Master"));
        _participantRepository
            .Setup(repository => repository.GetActiveByIdAsync(participantId))
            .ReturnsAsync((GameParticipant?)null);

        var act = async () => await _participantService.DeleteGameParticipantAsync(_gameId, participantId);

        await Assert.ThrowsAsync<NotFoundException>(act);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(It.IsAny<GameParticipant>()), Times.Never);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteGameParticipantAsync_WhenParticipantBelongsToAnotherGame_ThrowsNotFoundException()
    {
        var participantId = Guid.NewGuid();
        var anotherGameId = Guid.NewGuid();

        SetupCurrentUser(_masterId);
        _participantRepository
            .Setup(repository => repository.GetByUserIdAndGameIdAsync(_masterId, _gameId))
            .ReturnsAsync(CreateParticipant(_masterId, _gameId, ParticipantRole.Master, "Master"));
        _participantRepository
            .Setup(repository => repository.GetActiveByIdAsync(participantId))
            .ReturnsAsync(CreateParticipant(_playerId, anotherGameId, ParticipantRole.Player, "Player", participantId));

        var act = async () => await _participantService.DeleteGameParticipantAsync(_gameId, participantId);

        await Assert.ThrowsAsync<NotFoundException>(act);
        _participantRepository.Verify(repository => repository.RemoveGameParticipant(It.IsAny<GameParticipant>()), Times.Never);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteGameParticipantAsync_WhenCurrentUserIsMaster_RemovesParticipantAndSavesChanges()
    {
        var participantId = Guid.NewGuid();
        var participant = CreateParticipant(_playerId, _gameId, ParticipantRole.Player, "Player", participantId);

        SetupCurrentUser(_masterId);
        _participantRepository
            .Setup(repository => repository.GetByUserIdAndGameIdAsync(_masterId, _gameId))
            .ReturnsAsync(CreateParticipant(_masterId, _gameId, ParticipantRole.Master, "Master"));
        _participantRepository
            .Setup(repository => repository.GetActiveByIdAsync(participantId))
            .ReturnsAsync(participant);

        await _participantService.DeleteGameParticipantAsync(_gameId, participantId);

        _participantRepository.Verify(repository => repository.RemoveGameParticipant(participant), Times.Once);
        _participantRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    private void SetupCurrentUser(Guid userId)
    {
        _currentUserService.Setup(service => service.GetRequiredUserId()).Returns(userId);
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
