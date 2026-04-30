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

        var masterUser = await EnsureUserAsync(
            userManager,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "master@test.com",
            "Master123!",
            "Master");

        var playerUser = await EnsureUserAsync(
            userManager,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "player@test.com",
            "Player123!",
            "Player");

        var spectatorUser = await EnsureUserAsync(
            userManager,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "spectator@test.com",
            "Spectator123!",
            "Spectator");

        await SeedGameAsync(context, masterUser, playerUser, spectatorUser);
    }

    private static async Task<User> EnsureUserAsync(
        UserManager<User> userManager,
        Guid userId,
        string email,
        string password,
        string displayName)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is not null)
            return user;

        user = new User
        {
            Id = userId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            RefreshToken = null
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
            .Include(g => g.Issues)
            .FirstOrDefaultAsync(g => g.Id == Guid.Parse("44444444-4444-4444-4444-444444444444"));

        if (existingGame is not null)
            return;

        var game = new Game
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
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
            Participants = new List<GameParticipant>(),
            Issues = new List<Issue>()
        };

        var masterParticipant = new GameParticipant
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            GameId = game.Id,
            UserId = masterUser.Id,
            DisplayName = masterUser.DisplayName,
            Role = ParticipantRole.Master,
            JoinedAt = DateTime.UtcNow,
            IsConnected = true
        };

        var playerParticipant = new GameParticipant
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            GameId = game.Id,
            UserId = playerUser.Id,
            DisplayName = playerUser.DisplayName,
            Role = ParticipantRole.Player,
            JoinedAt = DateTime.UtcNow,
            IsConnected = true
        };

        var spectatorParticipant = new GameParticipant
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            GameId = game.Id,
            UserId = spectatorUser.Id,
            DisplayName = spectatorUser.DisplayName,
            Role = ParticipantRole.Spectator,
            JoinedAt = DateTime.UtcNow,
            IsConnected = true
        };

        game.Participants.Add(masterParticipant);
        game.Participants.Add(playerParticipant);
        game.Participants.Add(spectatorParticipant);

        game.Issues.Add(new Issue
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
            GameId = game.Id,
            Url = "https://app.plane.so/demo/projects/demo/issues/first",
            Title = "First issue",
            Description = "Add registration, login and JWT authentication.",
            Order = 1,
            IsCurrent = true,
            IsRemoved = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = masterParticipant.Id
        });

        game.Issues.Add(new Issue
        {
            Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
            GameId = game.Id,
            Url = "https://app.plane.so/demo/projects/demo/issues/second",
            Title = "Second issue",
            Description = "Create issues sidebar and basic issue actions.",
            Order = 2,
            IsCurrent = false,
            IsRemoved = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = masterParticipant.Id
        });

        game.Issues.Add(new Issue
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            GameId = game.Id,
            Url = "https://app.plane.so/demo/projects/demo/issues/third",
            Title = "Third issue",
            Description = "Implement voting flow and active issue selection.",
            Order = 3,
            IsCurrent = false,
            IsRemoved = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = masterParticipant.Id
        });

        await context.Games.AddAsync(game);
        await context.SaveChangesAsync();
    }
}
