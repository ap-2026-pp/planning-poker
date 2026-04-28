using PlanningPoker.Domain.DTOs.Game;
using QRCoder;

namespace PlanningPoker.API.Services;

public class InviteLinkService(IConfiguration configuration)
{
    private const int QrPixelsPerModule = 20;

    public GameInviteDto BuildInviteDto(Guid gameId, string inviteCode, HttpRequest request)
    {
        var inviteUrl = BuildInviteUrl(inviteCode, request);

        return new GameInviteDto
        {
            GameId = gameId,
            InviteCode = inviteCode,
            InviteUrl = inviteUrl,
            QrCodeBase64 = Convert.ToBase64String(
                PngByteQRCodeHelper.GetQRCode(inviteUrl, QRCodeGenerator.ECCLevel.Q, QrPixelsPerModule))
        };
    }

    private string BuildInviteUrl(string inviteCode, HttpRequest request)
    {
        var configuredBaseUrl = configuration["ClientApp:BaseUrl"]?.TrimEnd('/');
        var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? $"{request.Scheme}://{request.Host}"
            : configuredBaseUrl;

        return $"{baseUrl}/{Uri.EscapeDataString(inviteCode)}/";
    }
}
