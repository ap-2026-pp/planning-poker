using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.BLL.DTOs.Auth;
using PlanningPoker.Domain.DTOs.Auth;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(IUserService userService) : ControllerBase
    {
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var result = await userService.RegisterAsync(dto);
            return Ok(result);
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var result = await userService.LoginAsync(dto);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await userService.RevokeTokenAsync();
            return NoContent();
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            await userService.ChangePasswordAsync(dto);
            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] TokenRequestDto dto)
        {
            var result = await userService.RefreshTokensAsync(dto);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var result = await userService.GetCurrentUserAsync();
            return Ok(result);
        }

        [Authorize]
        [HttpPut("me/display-name")]
        public async Task<IActionResult> UpdateCurrentUserDisplayName([FromBody] UpdateUserDisplayNameDto dto)
        {
            var result = await userService.UpdateCurrentUserDisplayNameAsync(dto);
            return Ok(result);
        }
    }
}
