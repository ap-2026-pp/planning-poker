using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.DTOs.User;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController(IUserRepository userRepository): ControllerBase
{
    [HttpPost]
    public async Task<ActionResult> Post([FromBody] RegisterDto registerDto)
    {
        // Temporary stub until user registration flow is implemented.
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = registerDto.DisplayName,
            DisplayName = registerDto.DisplayName,
            RefreshToken = string.Empty
        };

        var createdUser = await userRepository.AddAsync(user);
        await userRepository.SaveChangesAsync();

        return Ok(new
        {
            id = createdUser.Id,
            displayName = createdUser.DisplayName,
            message = "Temporary user registration stub"
        });
    }
}
