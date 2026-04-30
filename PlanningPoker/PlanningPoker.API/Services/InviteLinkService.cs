using PlanningPoker.Domain.DTOs.Game;
using QRCoder;

namespace PlanningPoker.API.Services;

/// <summary>
/// Формує DTO для запрошення в гру, включно з посиланням та QR-кодом.
/// </summary>
public class InviteLinkService(IConfiguration configuration)
{
    private const int QrPixelsPerModule = 20;

    /// <summary>
    /// Створює <see cref="GameInviteDto"/> із кодом запрошення, посиланням на гру та QR-кодом.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="inviteCode">Код запрошення.</param>
    /// <param name="request">Поточний HTTP-запит для побудови базового URL.</param>
    /// <returns><see cref="GameInviteDto"/> з даними для запрошення до гри.</returns>
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
