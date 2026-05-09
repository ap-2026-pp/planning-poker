using Microsoft.AspNetCore.Mvc;
using Moq;
using PlanningPoker.API.Controllers;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.DTOs.Participant;
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
    public async Task JoinGameByInviteCode_WhenRequestIsValid_ReturnsOkWithGame()
    {
        const string inviteCode = "invite-code";
        var request = new JoinGameRequestDto { DisplayName = "Player" };
        var response = new JoinGameResponseDto
        {
            Game = CreateGameDto("Demo Game"),
            CurrentParticipantId = Guid.NewGuid(),
            GuestAccessToken = "guest-access-token"
        };

        _participantService
            .Setup(service => service.JoinGameByInviteCodeAsync(inviteCode, request))
            .ReturnsAsync(response);

        var result = await _controller.JoinGameByInviteCode(inviteCode, request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<JoinGameResponseDto>(okResult.Value);

        Assert.Same(response, payload);
        _participantService.Verify(service => service.JoinGameByInviteCodeAsync(inviteCode, request), Times.Once);
    }

    [Fact]
    public async Task ReconnectToGame_WhenRequestIsValid_ReturnsOkWithGame()
    {
        var gameId = Guid.NewGuid();
        var response = new JoinGameResponseDto
        {
            Game = CreateGameDto("Demo Game"),
            CurrentParticipantId = Guid.NewGuid()
        };

        _participantService
            .Setup(service => service.ReconnectToGameAsync(gameId))
            .ReturnsAsync(response);

        var result = await _controller.ReconnectToGame(gameId);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<JoinGameResponseDto>(okResult.Value);

        Assert.Same(response, payload);
        _participantService.Verify(service => service.ReconnectToGameAsync(gameId), Times.Once);
    }

    [Fact]
    public async Task LeaveGame_WhenServiceSucceeds_ReturnsNoContent()
    {
        var gameId = Guid.NewGuid();

        var result = await _controller.LeaveGame(gameId);

        Assert.IsType<NoContentResult>(result);
        _participantService.Verify(service => service.LeaveGameAsync(gameId), Times.Once);
    }

    [Fact]
    public async Task ChangeDisplayName_WhenRequestIsValid_ReturnsOkWithUpdatedParticipant()
    {
        var gameId = Guid.NewGuid();
        var request = new UpdateDisplayNameDto { DisplayName = "Updated Player" };
        var participant = CreateParticipantDto(request.DisplayName, ParticipantRole.Player);

        _participantService
            .Setup(service => service.UpdateDisplayNameAsync(gameId, request.DisplayName))
            .ReturnsAsync(participant);

        var result = await _controller.ChangeDisplayName(gameId, request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<GameParticipantDto>(okResult.Value);

        Assert.Same(participant, payload);
        _participantService.Verify(service => service.UpdateDisplayNameAsync(gameId, request.DisplayName), Times.Once);
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

    [Fact]
    public async Task TransferMaster_WhenServiceSucceeds_ReturnsNoContent()
    {
        var gameId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        var result = await _controller.TransferMaster(gameId, participantId);

        Assert.IsType<NoContentResult>(result);
        _participantService.Verify(service => service.TransferMasterAsync(gameId, participantId), Times.Once);
    }

    [Fact]
    public async Task TransferMaster_WhenUserDoesNotHaveRights_ThrowsException()
    {
        var gameId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        _participantService
            .Setup(service => service.TransferMasterAsync(gameId, participantId))
            .ThrowsAsync(new ForbiddenException("transfer master to", "participant"));

        var act = async () => await _controller.TransferMaster(gameId, participantId);

        await Assert.ThrowsAsync<ForbiddenException>(act);
        _participantService.Verify(service => service.TransferMasterAsync(gameId, participantId), Times.Once);
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

    private static GameDto CreateGameDto(string name)
    {
        return new GameDto
        {
            Id = Guid.NewGuid(),
            Name = name,
            InviteCode = "invite-code",
            VotingSystem = VotingSystem.Custom,
            AutoRevealCards = true,
            ShowAverage = true,
            ShowCountdownAnimation = true,
            IsActive = true,
            Participants = []
        };
    }
}

