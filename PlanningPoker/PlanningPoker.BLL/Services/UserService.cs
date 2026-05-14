using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using PlanningPoker.BLL.Constants;
using PlanningPoker.BLL.DTOs.Auth;
using PlanningPoker.Domain.DTOs.Auth;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

internal class UserService
    : IUserService
{
    private readonly UserManager<User> _userManager;
    private readonly IJwtService _jwtService;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ICookieService _cookieService;
    private readonly IGuestAccountLinkService _guestAccountLinkService;
    private readonly ICurrentUserContext _currentUserContext;
    public UserService(
        UserManager<User> userManager,
        IJwtService jwtService,
        ICurrentUserAccessor currentUser,
        ICookieService cookieService,
        IGuestAccountLinkService guestAccountLinkService,
        ICurrentUserContext currentUserContext)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _currentUser = currentUser;
        _cookieService = cookieService;
        _guestAccountLinkService = guestAccountLinkService;
        _currentUserContext = currentUserContext;
    }
    private void SetAuthCookies(string accessToken, string refreshToken, DateTime refreshTokenExpiry)
    {
        var refreshMinutes = (int)Math.Floor((refreshTokenExpiry - DateTime.UtcNow).TotalMinutes);

        _cookieService.SetTokenCookie("accessToken", accessToken, (int)JwtDefaults.ExpiresInMinutes);
        _cookieService.SetTokenCookie("refreshToken", refreshToken, refreshMinutes);
    }

    public async Task<UserDto> RegisterAsync(RegisterDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser is not null)
            throw new ResourceAlreadyExistsException(nameof(User), "email", dto.Email);

        var emailLocalPart = ExtractEmailLocalPart(dto.Email);

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = emailLocalPart,
            Email = dto.Email,
            DisplayName = emailLocalPart,
            CreatedAt = DateTime.UtcNow,
            RefreshToken = string.Empty
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        var refreshToken = _jwtService.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(JwtDefaults.RefreshTokenExpiresInDays);

        await _userManager.UpdateAsync(user);
        await _guestAccountLinkService.AttachGuestParticipantToUserAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtService.GenerateAccessToken(user.Id, user.Email!, roles);
        SetAuthCookies(accessToken, refreshToken, user.RefreshTokenExpiryTime.Value);
        return AuthMapper.ToUserDto(user);
    }

    public async Task<UserDto> LoginAsync(LoginDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid credentials.");

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!isPasswordValid)
            throw new UnauthorizedAccessException("Invalid credentials.");

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtService.GenerateAccessToken(user.Id, GetRequiredEmail(user), roles);
        var refreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(JwtDefaults.RefreshTokenExpiresInDays);

        await _userManager.UpdateAsync(user);
        await _guestAccountLinkService.AttachGuestParticipantToUserAsync(user);

        SetAuthCookies(accessToken, refreshToken, user.RefreshTokenExpiryTime.Value);
        return AuthMapper.ToUserDto(user);
    }

    public async Task RefreshTokenAsync()
    {
        var accessToken = _cookieService.GetCookie("accessToken")
                          ?? throw new SecurityTokenException("Access token not found in cookies.");
        var refreshToken = _cookieService.GetCookie("refreshToken")
                           ?? throw new SecurityTokenException("Refresh token not found in cookies.");

        var principal = _jwtService.GetPrincipalFromExpiredToken(accessToken);
        var email = principal.FindFirstValue(ClaimTypes.Email);

        var user = await _userManager.FindByEmailAsync(email!)
                   ?? throw new SecurityTokenException("User not found.");


        if (user.RefreshToken != refreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            throw new SecurityTokenException("Invalid or expired refresh token.");

        var roles = await _userManager.GetRolesAsync(user);
        var newAccessToken = _jwtService.GenerateAccessToken(user.Id, GetRequiredEmail(user), roles);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(JwtDefaults.RefreshTokenExpiresInDays);
        await _userManager.UpdateAsync(user);

        SetAuthCookies(newAccessToken, newRefreshToken, user.RefreshTokenExpiryTime.Value);
    }

    /// <summary>
    /// Відкликає refresh token поточного авторизованого користувача, очищаючи значення токена та строк його дії.
    /// </summary>
    /// <returns>Асинхронна операція без повернення значення.</returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Виникає, якщо не вдалося визначити поточного користувача.
    /// </exception>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо поточного користувача не знайдено в сховищі.
    /// </exception>
    public async Task RevokeTokenAsync()
    {
        var user = await _currentUserContext.GetRequiredUserAsync();

        user.RefreshToken = string.Empty;
        user.RefreshTokenExpiryTime = null;
        await _userManager.UpdateAsync(user);

        _cookieService.DeleteCookie("accessToken");
        _cookieService.DeleteCookie("refreshToken");

        await _userManager.UpdateAsync(user);
    }

    /// <summary>
    /// Повертає профіль поточного авторизованого користувача.
    /// </summary>
    /// <returns>Дані поточного користувача у вигляді <see cref="UserDto"/>.</returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Виникає, якщо не вдалося визначити поточного користувача.
    /// </exception>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо поточного користувача не знайдено в сховищі.
    /// </exception>
    public async Task<UserDto> GetCurrentUserAsync()
    {
        var user = await _currentUserContext.GetRequiredUserAsync();
        return AuthMapper.ToUserDto(user);
    }

    /// <summary>
    /// Оновлює глобальне display name поточного авторизованого користувача.
    /// Це значення використовується як профільне ім'я за замовчуванням,
    /// але не змінює display name у вже наявних game participants.
    /// </summary>
    /// <param name="dto">Нове глобальне display name користувача.</param>
    /// <returns>Оновлений профіль користувача у вигляді <see cref="UserDto"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Виникає, якщо вхідний DTO дорівнює <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Виникає, якщо нове display name порожнє або складається лише з пробілів,
    /// або якщо не вдалося зберегти зміни користувача.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Виникає, якщо не вдалося визначити поточного користувача.
    /// </exception>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо поточного користувача не знайдено в сховищі.
    /// </exception>
    public async Task<UserDto> UpdateCurrentUserDisplayNameAsync(UpdateUserDisplayNameDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var displayName = dto.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException("Display name is required.");
        }

        var user = await _currentUserContext.GetRequiredUserAsync();
        if (string.Equals(user.DisplayName, displayName, StringComparison.Ordinal))
        {
            return AuthMapper.ToUserDto(user);
        }

        user.DisplayName = displayName;

        var result = await _userManager.UpdateAsync(user);
        return !result.Succeeded ? throw CreateIdentityOperationException(result) : AuthMapper.ToUserDto(user);
    }

    /// <summary>
    /// Змінює пароль поточного авторизованого користувача.
    /// Після успішної зміни пароля відкликає refresh token, щоб користувач повторно пройшов авторизацію.
    /// </summary>
    /// <param name="dto">Старий і новий пароль користувача.</param>
    /// <returns>Асинхронна операція без повернення значення.</returns>
    /// <exception cref="ArgumentNullException">
    /// Виникає, якщо вхідний DTO дорівнює <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Виникає, якщо новий пароль збігається з поточним
    /// або якщо операція зміни пароля / оновлення користувача завершилася з помилкою.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Виникає, якщо не вдалося визначити поточного користувача.
    /// </exception>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо поточного користувача не знайдено в сховищі.
    /// </exception>
    public async Task ChangePasswordAsync(ChangePasswordDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.OldPassword == dto.NewPassword)
            throw new InvalidOperationException("New password must be different from current password.");

        var user = await _currentUserContext.GetRequiredUserAsync();

        var result = await _userManager.ChangePasswordAsync(user, dto.OldPassword, dto.NewPassword);

        if (!result.Succeeded)
            throw CreateIdentityOperationException(result);

        user.RefreshToken = string.Empty;
        user.RefreshTokenExpiryTime = null;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            throw CreateIdentityOperationException(updateResult);
    }

    /// <summary>
    /// Повертає email користувача або викидає виняток, якщо email відсутній.
    /// </summary>
    /// <param name="user">Користувач, для якого потрібно отримати email.</param>
    /// <returns>Email користувача.</returns>
    /// <exception cref="InvalidOperationException">
    /// Виникає, якщо email користувача відсутній.
    /// </exception>
    private static string GetRequiredEmail(User user)
    {
        return user.Email
            ?? throw new InvalidOperationException("User email is missing.");
    }

    /// <summary>
    /// Формує виняток <see cref="InvalidOperationException"/> на основі помилок, повернених Identity-операцією.
    /// </summary>
    /// <param name="result">Результат операції Identity.</param>
    /// <returns>
    /// Екземпляр <see cref="InvalidOperationException"/> з об’єднаним текстом усіх помилок.
    /// </returns>
    private static InvalidOperationException CreateIdentityOperationException(IdentityResult result)
    {
        var message = string.Join("; ", result.Errors.Select(error => error.Description));
        return new InvalidOperationException(message);
    }

    private static string ExtractEmailLocalPart(string email)
    {
        var separatorIndex = email.IndexOf('@');
        return separatorIndex > 0 ? email[..separatorIndex] : email;
    }
}
