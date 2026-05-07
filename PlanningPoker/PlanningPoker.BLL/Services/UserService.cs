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

internal class UserService(
    UserManager<User> userManager,
    IJwtService jwtService,
    ICurrentUserContext currentUserContext,
    IGuestAccountLinkService guestAccountLinkService)
    : IUserService
{
    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var existingUser = await userManager.FindByEmailAsync(dto.Email);
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

        var result = await userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        var refreshToken = jwtService.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(JwtDefaults.RefreshTokenExpiresInDays);

        await userManager.UpdateAsync(user);
        await guestAccountLinkService.AttachGuestParticipantToUserAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtService.GenerateAccessToken(user.Id, GetRequiredEmail(user), roles);

        return AuthMapper.ToAuthResponseDto(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(JwtDefaults.ExpiresInMinutes),
            user
        );
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid credentials.");

        var isPasswordValid = await userManager.CheckPasswordAsync(user, dto.Password);
        if (!isPasswordValid)
            throw new UnauthorizedAccessException("Invalid credentials.");

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtService.GenerateAccessToken(user.Id, GetRequiredEmail(user), roles);
        var refreshToken = jwtService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(JwtDefaults.RefreshTokenExpiresInDays);

        await userManager.UpdateAsync(user);
        await guestAccountLinkService.AttachGuestParticipantToUserAsync(user);

        return AuthMapper.ToAuthResponseDto(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(JwtDefaults.ExpiresInMinutes),
            user
        );
    }

    public async Task<AuthResponseDto> RefreshTokensAsync(TokenRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var principal = jwtService.GetPrincipalFromExpiredToken(dto.AccessToken);
        var email = principal.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(email))
            throw new SecurityTokenException("Invalid token.");

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            throw new SecurityTokenException("User not found.");

        if (user.RefreshToken != dto.RefreshToken)
            throw new SecurityTokenException("Invalid refresh token.");

        if (user.RefreshTokenExpiryTime is null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            throw new SecurityTokenException("Refresh token expired.");

        var roles = await userManager.GetRolesAsync(user);
        var newAccessToken = jwtService.GenerateAccessToken(user.Id, GetRequiredEmail(user), roles);
        var newRefreshToken = jwtService.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(JwtDefaults.RefreshTokenExpiresInDays);

        await userManager.UpdateAsync(user);

        return AuthMapper.ToAuthResponseDto(
            newAccessToken,
            newRefreshToken,
            DateTime.UtcNow.AddMinutes(JwtDefaults.ExpiresInMinutes),
            user
        );
    }

    public async Task RevokeTokenAsync()
    {
        var user = await currentUserContext.GetRequiredUserAsync();

        user.RefreshToken = string.Empty;
        user.RefreshTokenExpiryTime = null;

        await userManager.UpdateAsync(user);
    }

    public async Task<UserDto> GetCurrentUserAsync()
    {
        var user = await currentUserContext.GetRequiredUserAsync();

        return AuthMapper.ToUserDto(user);
    }

    private static string GetRequiredEmail(User user)
    {
        return user.Email
            ?? throw new InvalidOperationException("User email is missing.");
    }

    private static string ExtractEmailLocalPart(string email)
    {
        var separatorIndex = email.IndexOf('@');
        return separatorIndex > 0 ? email[..separatorIndex] : email;
    }
}
