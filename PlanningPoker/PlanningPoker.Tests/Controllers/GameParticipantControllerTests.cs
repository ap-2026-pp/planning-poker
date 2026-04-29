using Microsoft.AspNetCore.Mvc;
using Moq;
using PlanningPoker.API.Controllers;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Tests.Controllers;

public class GameParticipantControllerTests
{
    private readonly Mock<IParticipantService> _participantService = new();
    private readonly GameParticipantController _controller;

    public GameParticipantControllerTests()
    {
        _controller = new GameParticipantController(_participantService.Object);
    }

    [Fact]
    public async Task GetGameParticipants_WhenParticipantsExist_ReturnsOkWithParticipants()
    {
        var gameId = Guid.NewGuid();
        var participants = new List<GameParticipantDto>
        {
            CreateParticipantDto("Master", ParticipantRole.Master),
            CreateParticipantDto("Player", ParticipantRole.Player)
        };

        _participantService
            .Setup(service => service.GetGameParticipantsAsync(gameId))
            .ReturnsAsync(participants);

        var result = await _controller.GetGameParticipants(gameId);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<GameParticipantDto>>(okResult.Value);

        Assert.Equal(2, payload.Count());
        Assert.Same(participants, payload);
        _participantService.Verify(service => service.GetGameParticipantsAsync(gameId), Times.Once);
    }

    [Fact]
    public async Task DeleteGameParticipant_WhenUserHasRights_ReturnsNoContent()
    {
        var gameId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        var result = await _controller.DeleteGameParticipant(gameId, participantId);

        Assert.IsType<NoContentResult>(result);
        _participantService.Verify(service => service.DeleteGameParticipantAsync(gameId, participantId), Times.Once);
    }

    [Fact]
    public async Task DeleteGameParticipant_WhenUserDoesNotHaveRights_ThrowsException()
    {
        var gameId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        _participantService
            .Setup(service => service.DeleteGameParticipantAsync(gameId, participantId))
            .ThrowsAsync(new ForbiddenException("delete", "participant"));

        var act = async () => await _controller.DeleteGameParticipant(gameId, participantId);

        await Assert.ThrowsAsync<ForbiddenException>(act);
        _participantService.Verify(service => service.DeleteGameParticipantAsync(gameId, participantId), Times.Once);
    }

    private static GameParticipantDto CreateParticipantDto(string displayName, ParticipantRole role)
    {
        return new GameParticipantDto
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DisplayName = displayName,
            Role = role,
            IsConnected = true,
            JoinedAt = DateTime.UtcNow
        };
    }
}
