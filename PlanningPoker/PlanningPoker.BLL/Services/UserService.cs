using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using PlanningPoker.BLL.Constants;
using PlanningPoker.BLL.DTOs.Auth;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

internal class UserService : IUserService
{
    private readonly UserManager<User> _userManager;
    private readonly IJwtService _jwtService;

    public UserService(UserManager<User> userManager, IJwtService jwtService)
    {
        _userManager = userManager;
        _jwtService = jwtService;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
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

        return AuthMapper.ToAuthResponseDto(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(JwtDefaults.ExpiresInMinutes),
            user
        );
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
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

        return AuthMapper.ToAuthResponseDto(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(JwtDefaults.ExpiresInMinutes),
            user
        );
    }

    public async Task<AuthResponseDto> RefreshTokensAsync(TokenRequestDto dto)
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto));

        var principal = _jwtService.GetPrincipalFromExpiredToken(dto.AccessToken);
        var email = principal.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(email))
            throw new SecurityTokenException("Invalid token.");

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            throw new SecurityTokenException("User not found.");

        if (user.RefreshToken != dto.RefreshToken)
            throw new SecurityTokenException("Invalid refresh token.");

        if (user.RefreshTokenExpiryTime is null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            throw new SecurityTokenException("Refresh token expired.");

        var roles = await _userManager.GetRolesAsync(user);
        var newAccessToken = _jwtService.GenerateAccessToken(user.Id, user.Email!, roles);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(JwtDefaults.RefreshTokenExpiresInDays);

        await _userManager.UpdateAsync(user);

        return AuthMapper.ToAuthResponseDto(
            newAccessToken,
            newRefreshToken,
            DateTime.UtcNow.AddMinutes(JwtDefaults.ExpiresInMinutes),
            user
        );
    }

    public async Task RevokeTokenAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new KeyNotFoundException("User not found.");

        user.RefreshToken = string.Empty;
        user.RefreshTokenExpiryTime = null;

        await _userManager.UpdateAsync(user);
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new KeyNotFoundException("User not found.");

        return AuthMapper.ToUserDto(user);
    }
}
