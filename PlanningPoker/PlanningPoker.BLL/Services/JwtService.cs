using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PlanningPoker.BLL.Constants;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.BLL.Services;

internal class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Ініціалізує сервіс JWT з конфігурації застосунку.
    /// </summary>
    /// <param name="configuration">Конфігурація (Jwt Key, Issuer, Audience тощо).</param>
    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Генерує access JWT токен для користувача.
    /// </summary>
    /// <param name="userId">Id користувача.</param>
    /// <param name="email">Email користувача.</param>
    /// <param name="roles">Список ролей користувача.</param>
    /// <returns>JWT access token.</returns>
    public string GenerateAccessToken(Guid userId, string email, IList<string> roles)
    {
        var keyString = GetRequiredConfig("Jwt:Key");
        var issuer = GetRequiredConfig("Jwt:Issuer");
        var audience = GetRequiredConfig("Jwt:Audience");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresInMinutes = double.TryParse(_configuration["Jwt:ExpiresInMinutes"], out var minutes)
            ? minutes
            : JwtDefaults.ExpiresInMinutes;

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Генерує refresh token для користувача.
    /// </summary>
    /// <returns>Base64 refresh token.</returns>
    public string GenerateRefreshToken()
    {
        var bytes = new byte[JwtDefaults.RefreshTokenBytesLength];

        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Отримує ClaimsPrincipal із простроченого JWT токена.
    /// Використовується для оновлення access token.
    /// </summary>
    /// <param name="token">JWT access token.</param>
    /// <returns>ClaimsPrincipal з токена.</returns>
    /// <exception cref="SecurityTokenException">Якщо токен невалідний.</exception>
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

        var principal = tokenHandler.ValidateToken(
            token,
            tokenValidationParameters,
            out var securityToken);

        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            !jwtSecurityToken.Header.Alg.Equals(
                SecurityAlgorithms.HmacSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenException("Invalid token.");
        }

        return principal;
    }

    /// <summary>
    /// Отримує обов’язкову конфігурацію за ключем.
    /// </summary>
    /// <param name="key">Ключ конфігурації.</param>
    /// <returns>Значення конфігурації.</returns>
    /// <exception cref="InvalidOperationException">Якщо ключ відсутній.</exception>
    private string GetRequiredConfig(string key)
    {
        return _configuration[key]
            ?? throw new InvalidOperationException($"{key} is not configured.");
    }
}