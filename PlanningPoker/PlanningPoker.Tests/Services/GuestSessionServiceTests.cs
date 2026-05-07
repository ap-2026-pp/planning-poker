using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Moq;
using PlanningPoker.BLL.Services;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Tests.Services;

public class GuestSessionServiceTests
{
    private readonly Mock<IGuestSessionRepository> _repository = new();
    private readonly GuestSessionService _guestSessionService;

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
        _repository.Verify(repository => repository.AddAsync(It.IsAny<GuestSession>()), Times.Once);
        _repository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public void GenerateGuestAccessToken_WhenParticipantIdIsProvided_ReturnsJwtWithParticipantClaim()
    {
        var participantId = Guid.NewGuid();

        var token = _guestSessionService.GenerateGuestAccessToken(participantId);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(participantId.ToString(), jwt.Claims.Single(claim => claim.Type == "participant_id").Value);
        Assert.Equal("guest_access", jwt.Claims.Single(claim => claim.Type == "token_type").Value);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type == ClaimTypes.NameIdentifier);
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WhenTokenIsValid_ReturnsPrincipal()
    {
        var participantId = Guid.NewGuid();
        var token = _guestSessionService.GenerateGuestAccessToken(participantId);

        var principal = _guestSessionService.GetPrincipalFromExpiredToken(token);

        Assert.Equal(participantId.ToString(), principal.FindFirstValue("participant_id"));
        Assert.Equal("guest_access", principal.FindFirstValue("token_type"));
    }
}
