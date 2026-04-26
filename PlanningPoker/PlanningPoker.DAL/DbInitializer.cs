using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Data;

public static class DbInitializer
{
    public static async Task SeedDataAsync(
        AppDbContext context,
        UserManager<User> userManager)
    {
        await context.Database.MigrateAsync();

        var masterUser = await EnsureUserAsync(userManager, "master@test.com", "Master123!", "Master");
        var playerUser = await EnsureUserAsync(userManager, "player@test.com", "Player123!", "Player");
        var spectatorUser = await EnsureUserAsync(userManager, "spectator@test.com", "Spectator123!", "Spectator");


        await SeedGameAsync(context, masterUser, playerUser, spectatorUser);
    }
  
    private static async Task EnsureRoleAsync(RoleManager<Role> roleManager, string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
            return;

        var result = await roleManager.CreateAsync(new Role
        {
            Name = roleName,
            NormalizedName = roleName.ToUpperInvariant()
        });

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create role '{roleName}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    private static async Task<User> EnsureUserAsync(
        UserManager<User> userManager,
        string email,
        string password,
        string displayName)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is not null)
            return user;

        user = new User
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            RefreshToken = string.Empty
        };

        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create seed user '{email}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    private static async Task SeedGameAsync(
        AppDbContext context,
        User masterUser,
        User playerUser,
        User spectatorUser)
    {
        var existingGame = await context.Games
            .Include(g => g.Participants)
            .FirstOrDefaultAsync(g => g.Name == "Demo Planning Poker");

        if (existingGame is not null)
            return;

        var game = new Game
        {
            Id = Guid.NewGuid(),
            Name = "Demo Planning Poker",
            VotingSystem = VotingSystem.Fibonacci,
            InviteCode = "DEMO123",
            AutoRevealCards = false,
            ShowAverage = true,
            ShowCountdownAnimation = true,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsDeleted = false,
            CreatedBy = masterUser.Id,
            Participants = new List<GameParticipant>()
        };

        game.Participants.Add(new GameParticipant
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            UserId = masterUser.Id,
            DisplayName = masterUser.DisplayName,
            Role = ParticipantRole.Master,
            JoinedAt = DateTime.UtcNow,
            IsConnected = true
        });

        game.Participants.Add(new GameParticipant
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            UserId = playerUser.Id,
            DisplayName = playerUser.DisplayName,
            Role = ParticipantRole.Player,
            JoinedAt = DateTime.UtcNow,
            IsConnected = true
        });

        game.Participants.Add(new GameParticipant
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            UserId = spectatorUser.Id,
            DisplayName = spectatorUser.DisplayName,
            Role = ParticipantRole.Spectator,
            JoinedAt = DateTime.UtcNow,
            IsConnected = true
        });

        await context.Games.AddAsync(game);
        await context.SaveChangesAsync();
    }
}
