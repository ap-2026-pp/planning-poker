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

/// <summary>
/// Сервіс для керування користувачами: реєстрація, авторизація, оновлення токенів,
/// профілю та пароля.
/// </summary>
internal class UserService : IUserService
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

    /// <summary>
    /// Реєструє нового користувача та автоматично створює JWT токени.
    /// </summary>
    /// <param name="dto">Дані для реєстрації користувача.</param>
    /// <returns>Створений користувач.</returns>
    /// <exception cref="ResourceAlreadyExistsException">Виникає, якщо email вже зайнятий.</exception>
    /// <exception cref="InvalidOperationException">Виникає, якщо не вдалося створити користувача.</exception>
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

    /// <summary>
    /// Виконує авторизацію користувача за email і паролем.
    /// </summary>
    /// <param name="dto">Дані для входу.</param>
    /// <returns>Авторизований користувач.</returns>
    /// <exception cref="UnauthorizedAccessException">Виникає при неправильних облікових даних.</exception>
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

    /// <summary>
    /// Оновлює access та refresh токени користувача.
    /// </summary>
    /// <exception cref="SecurityTokenException">Виникає при відсутніх або невалідних токенах.</exception>
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
    /// Відкликає refresh token поточного користувача та видаляє cookies.
    /// </summary>
    public async Task RevokeTokenAsync()
    {
        var user = await _currentUserContext.GetRequiredUserAsync();

        user.RefreshToken = string.Empty;
        user.RefreshTokenExpiryTime = null;

        await _userManager.UpdateAsync(user);

        _cookieService.DeleteCookie("accessToken");
        _cookieService.DeleteCookie("refreshToken");
    }

    /// <summary>
    /// Отримує поточного авторизованого користувача.
    /// </summary>
    public async Task<UserDto> GetCurrentUserAsync()
    {
        var user = await _currentUserContext.GetRequiredUserAsync();
        return AuthMapper.ToUserDto(user);
    }

    /// <summary>
    /// Оновлює display name поточного користувача.
    /// </summary>
    public async Task<UserDto> UpdateCurrentUserDisplayNameAsync(UpdateUserDisplayNameDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var displayName = dto.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvalidOperationException("Display name is required.");

        var user = await _currentUserContext.GetRequiredUserAsync();

        if (string.Equals(user.DisplayName, displayName, StringComparison.Ordinal))
            return AuthMapper.ToUserDto(user);

        user.DisplayName = displayName;

        var result = await _userManager.UpdateAsync(user);
        return !result.Succeeded
            ? throw CreateIdentityOperationException(result)
            : AuthMapper.ToUserDto(user);
    }

    /// <summary>
    /// Змінює пароль поточного користувача.
    /// </summary>
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
    /// Повертає email користувача або кидає виняток.
    /// </summary>
    private static string GetRequiredEmail(User user)
        => user.Email ?? throw new InvalidOperationException("User email is missing.");

    /// <summary>
    /// Формує виняток на основі помилок Identity.
    /// </summary>
    private static InvalidOperationException CreateIdentityOperationException(IdentityResult result)
        => new(string.Join("; ", result.Errors.Select(e => e.Description)));

    private static string ExtractEmailLocalPart(string email)
    {
        var index = email.IndexOf('@');
        return index > 0 ? email[..index] : email;
    }
}