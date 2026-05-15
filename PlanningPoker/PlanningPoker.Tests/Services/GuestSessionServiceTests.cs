using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Moq;
using PlanningPoker.BLL.Services;
using PlanningPoker.Domain.Constants;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Tests.Services;

public class GuestSessionServiceTests
{
    private readonly Mock<IGuestSessionRepository> _repository = new();
    private readonly IGuestSessionService _guestSessionService;

    public GuestSessionServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "super_secret_key_1234567890_super_secret_key",
                ["Jwt:Issuer"] = "PlanningPokerApi",
                ["Jwt:Audience"] = "PlanningPokerClient",
                ["Jwt:ExpiresInMinutes"] = "60"
            })
            .Build();

        _guestSessionService = new GuestSessionService(_repository.Object, configuration);
    }

    [Fact]
    public async Task CreateGuestSession_WhenAccessTokenIsProvided_CreatesSessionWithHashedToken()
    {
        var participantId = Guid.NewGuid();
        const string accessToken = "guest-access-token";

        var session = await _guestSessionService.CreateGuestSession(participantId, accessToken);

        Assert.Equal(participantId, session.ParticipantId);
        Assert.NotEqual(accessToken, session.TokenHash);
        Assert.False(session.IsRevoked);
        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.True(session.ExpiresAt > DateTime.UtcNow.AddMinutes(50));

        _repository.Verify(repository => repository.AddAsync(It.Is<GuestSession>(createdSession =>
            createdSession.Id != Guid.Empty &&
            createdSession.ParticipantId == participantId &&
            createdSession.TokenHash != accessToken &&
            !createdSession.IsRevoked &&
            createdSession.ExpiresAt > DateTime.UtcNow.AddMinutes(50))), Times.Once);

        _repository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public void GenerateGuestAccessToken_WhenParticipantIdIsProvided_ReturnsJwtWithParticipantClaim()
    {
        var participantId = Guid.NewGuid();

        var token = _guestSessionService.GenerateGuestAccessToken(participantId);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(
            participantId.ToString(),
            jwt.Claims.Single(claim => claim.Type == GuestSessionDefaults.ParticipantIdClaimType).Value);

        Assert.Equal(
            GuestSessionDefaults.GuestAccessTokenType,
            jwt.Claims.Single(claim => claim.Type == GuestSessionDefaults.TokenTypeClaimType).Value);

        Assert.DoesNotContain(jwt.Claims, claim => claim.Type == ClaimTypes.NameIdentifier);
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WhenTokenIsValid_ReturnsPrincipal()
    {
        var participantId = Guid.NewGuid();
        var token = _guestSessionService.GenerateGuestAccessToken(participantId);

        var principal = _guestSessionService.GetPrincipalFromExpiredToken(token);

        Assert.Equal(
            participantId.ToString(),
            principal.FindFirstValue(GuestSessionDefaults.ParticipantIdClaimType));

        Assert.Equal(
            GuestSessionDefaults.GuestAccessTokenType,
            principal.FindFirstValue(GuestSessionDefaults.TokenTypeClaimType));
    }

    [Fact]
    public async Task IsGuestAccessTokenActiveAsync_WhenRepositoryReturnsSession_ReturnsTrue()
    {
        const string accessToken = "guest-access-token";

        _repository
            .Setup(repository => repository.GetActiveByTokenHashAsync(
                It.Is<string>(hash => !string.IsNullOrWhiteSpace(hash) && hash != accessToken),
                It.IsAny<DateTime>()))
            .ReturnsAsync(new GuestSession
            {
                Id = Guid.NewGuid(),
                ParticipantId = Guid.NewGuid(),
                TokenHash = "hashed-token",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                IsRevoked = false
            });

        var result = await _guestSessionService.IsGuestAccessTokenActiveAsync(accessToken);

        Assert.True(result);

        _repository.Verify(repository => repository.GetActiveByTokenHashAsync(
            It.Is<string>(hash => !string.IsNullOrWhiteSpace(hash) && hash != accessToken),
            It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task IsGuestAccessTokenActiveAsync_WhenRepositoryReturnsNull_ReturnsFalse()
    {
        const string accessToken = "guest-access-token";

        _repository
            .Setup(repository => repository.GetActiveByTokenHashAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync((GuestSession?)null);

        var result = await _guestSessionService.IsGuestAccessTokenActiveAsync(accessToken);

        Assert.False(result);
    }

    [Fact]
    public async Task RevokeGuestSessionsAsync_WhenParticipantIdIsProvided_RevokesSessionsAndSaves()
    {
        var participantId = Guid.NewGuid();

        await _guestSessionService.RevokeGuestSessionsAsync(participantId);

        _repository.Verify(repository => repository.RevokeActiveByParticipantIdAsync(
            participantId,
            It.IsAny<DateTime>()), Times.Once);

        _repository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }
}