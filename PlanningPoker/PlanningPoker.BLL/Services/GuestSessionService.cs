using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PlanningPoker.BLL.Constants;
using PlanningPoker.Domain.Constants;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Керує життєвим циклом гостьових сесій: генерацією токенів,
/// збереженням їх відбитків і перевіркою активності.
/// </summary>
public class GuestSessionService(
    IGuestSessionRepository guestSessionRepository,
    IConfiguration configuration) : IGuestSessionService
{
    
    /// <summary>
    /// Генерує guest access token для конкретного учасника гри.
    /// </summary>
    /// <param name="participantId">Ідентифікатор гостьового учасника.</param>
    /// <returns>Підписаний JWT access token.</returns>
    public string GenerateGuestAccessToken(Guid participantId)
    {
        var keyString = GetRequiredConfig("Jwt:Key");
        var issuer = GetRequiredConfig("Jwt:Issuer");
        var audience = GetRequiredConfig("Jwt:Audience");

        var claims = new List<Claim>
        {
            new(GuestSessionDefaults.ParticipantIdClaimType, participantId.ToString()),
            new(GuestSessionDefaults.TokenTypeClaimType, GuestSessionDefaults.GuestAccessTokenType),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(GetExpiresInMinutes()),
            signingCredentials: CreateSigningCredentials(keyString));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Створює запис гостьової сесії для виданого токена доступу.
    /// Зберігається лише хеш токена.
    /// </summary>
    /// <param name="participantId">Ідентифікатор учасника, якому належить сесія.</param>
    /// <param name="accessToken">Виданий токен доступу.</param>
    /// <returns>Створену сесію <see cref="GuestSession"/>.</returns>
    public async Task<GuestSession> CreateGuestSession(Guid participantId, string accessToken)
    {
        var session = new GuestSession
        {
            Id = Guid.NewGuid(),
            ParticipantId = participantId,
            TokenHash = ComputeTokenHash(accessToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(GetExpiresInMinutes()),
            IsRevoked = false
        };

        await guestSessionRepository.AddAsync(session);
        await guestSessionRepository.SaveChangesAsync();

        return session;
    }

    /// <summary>
    /// Перевіряє, чи існує активна та не відкликана гостьова сесія для вказаного токена.
    /// </summary>
    /// <param name="accessToken">Токен доступу для перевірки.</param>
    /// <returns><see langword="true"/>, якщо токен активний; інакше <see langword="false"/>.</returns>
    public async Task<bool> IsGuestAccessTokenActiveAsync(string accessToken)
    {
        var tokenHash = ComputeTokenHash(accessToken);
        var session = await guestSessionRepository.GetActiveByTokenHashAsync(tokenHash, DateTime.UtcNow);
        return session is not null;
    }

    /// <summary>
    /// Відкликає активну guest session для учасника.
    /// </summary>
    /// <param name="participantId">Ідентифікаційний код учасника.</param>
    public async Task RevokeGuestSessionsAsync(Guid participantId)
    {
        await guestSessionRepository.RevokeActiveByParticipantIdAsync(participantId, DateTime.UtcNow);
        await guestSessionRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Відновлює claims principal із токена без перевірки строку дії.
    /// Використовується для сценаріїв, де потрібно прочитати claims простроченого токена.
    /// </summary>
    /// <param name="token">JWT токен.</param>
    /// <returns><see cref="ClaimsPrincipal"/> з claims токена.</returns>
    /// <exception cref="SecurityTokenException">
    /// Виникає, якщо токен невалідний або підписаний неочікуваним алгоритмом.
    /// </exception>
    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        var keyString = GetRequiredConfig("Jwt:Key");
        var issuer = GetRequiredConfig("Jwt:Issuer");
        var audience = GetRequiredConfig("Jwt:Audience");

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString)),
            ClockSkew = TimeSpan.Zero
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenException("Invalid token.");
        }

        return principal;
    }

    private double GetExpiresInMinutes()
    {
        return double.TryParse(configuration["Jwt:ExpiresInMinutes"], out var minutes)
            ? minutes
            : JwtDefaults.ExpiresInMinutes;
    }

    private string GetRequiredConfig(string key)
    {
        return configuration[key]
            ?? throw new InvalidOperationException($"{key} is not configured.");
    }

    private static SigningCredentials CreateSigningCredentials(string keyString)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        return new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    private static string ComputeTokenHash(string accessToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(accessToken));
        return Convert.ToHexString(hashBytes);
    }
}
