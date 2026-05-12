using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using PlanningPoker.BLL.Constants;
using PlanningPoker.BLL.DTOs.Auth;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

internal class UserService : IUserService
{
    private readonly UserManager<User> _userManager;
    private readonly IJwtService _jwtService;
    private readonly ICurrentUserAccessor  _currentUser;
    private readonly ICookieService _cookieService;
    
    public UserService(
        UserManager<User> userManager,
        IJwtService jwtService,
        ICurrentUserAccessor  currentUser,
        ICookieService cookieService)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _currentUser = currentUser;
        _cookieService = cookieService;
    }
    private void SetAuthCookies(string accessToken, string refreshToken, DateTime refreshTokenExpiry)
    {
        var refreshMinutes = (int)Math.Floor((refreshTokenExpiry - DateTime.UtcNow).TotalMinutes);
        
        _cookieService.SetTokenCookie("accessToken", accessToken, (int)JwtDefaults.ExpiresInMinutes);
        _cookieService.SetTokenCookie("refreshToken", refreshToken, refreshMinutes);
    }

    public async Task<UserDto> RegisterAsync(RegisterDto dto)
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto));

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser is not null)
            throw new InvalidOperationException("User with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = dto.Email,
            Email = dto.Email,
            DisplayName = dto.Email,
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

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtService.GenerateAccessToken(user.Id, user.Email!, roles);
        SetAuthCookies(accessToken, refreshToken, user.RefreshTokenExpiryTime.Value);
        return AuthMapper.ToUserDto(user);
    }

    public async Task<UserDto> LoginAsync(LoginDto dto)
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto));

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid credentials.");

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!isPasswordValid)
            throw new UnauthorizedAccessException("Invalid credentials.");

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtService.GenerateAccessToken(user.Id, user.Email!, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(JwtDefaults.RefreshTokenExpiresInDays);
        await _userManager.UpdateAsync(user);

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
        var newAccessToken = _jwtService.GenerateAccessToken(user.Id, user.Email!, roles);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(JwtDefaults.RefreshTokenExpiresInDays);
        await _userManager.UpdateAsync(user);

        SetAuthCookies(newAccessToken, newRefreshToken, user.RefreshTokenExpiryTime.Value);
    }

    public async Task RevokeTokenAsync()
    {
        var userId = _currentUser.GetRequiredUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new NotFoundException(nameof(User), userId.ToString());

        user.RefreshToken = string.Empty;
        user.RefreshTokenExpiryTime = null; 
        await _userManager.UpdateAsync(user);
        
        _cookieService.DeleteCookie("accessToken");
        _cookieService.DeleteCookie("refreshToken");

        await _userManager.UpdateAsync(user);
    }

    public async Task<UserDto> GetCurrentUserAsync()
    {
        var userId = _currentUser.GetRequiredUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new NotFoundException(nameof(User), userId.ToString());

        return AuthMapper.ToUserDto(user);
    }
}
