using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Data;

public static class DbInitializer
{
    private static readonly Guid MasterUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PlayerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SpectatorUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly Guid DemoGameId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly Guid MasterParticipantId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid PlayerParticipantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid SpectatorParticipantId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static readonly Guid FirstIssueId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid SecondIssueId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid ThirdIssueId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static async Task SeedDataAsync(
        AppDbContext context,
        UserManager<User> userManager)
    {
        await context.Database.MigrateAsync();

        var masterUser = await EnsureUserAsync(
            userManager,
            MasterUserId,
            "master@test.com",
            "Master123!",
            "Master");

        var playerUser = await EnsureUserAsync(
            userManager,
            PlayerUserId,
            "player@test.com",
            "Player123!",
            "Player");

        var spectatorUser = await EnsureUserAsync(
            userManager,
            SpectatorUserId,
            "spectator@test.com",
            "Spectator123!",
            "Spectator");

        await SeedGameAsync(context, masterUser, playerUser, spectatorUser);

        await SeedVotingHistoryAsync(context);
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
        {
            return user;
        }

        user = new User
        {
            Id = userId,
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
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));

            throw new InvalidOperationException(
                $"Failed to create seed user '{email}': {errors}");
        }

        return user;
    }

    private static async Task SeedGameAsync(
        AppDbContext context,
        User masterUser,
        User playerUser,
        User spectatorUser)
    {
        const string demoGameName = "Demo Planning Poker";
        const string demoInviteCode = "DEMO123";

        var gameAlreadyExists = await context.Games.AnyAsync(game =>
            game.Id == DemoGameId ||
            game.InviteCode == demoInviteCode ||
            (game.Name == demoGameName &&
             game.CreatedBy == masterUser.Id &&
             !game.IsDeleted));

        if (gameAlreadyExists)
        {
            return;
        }

        var game = new Game
        {
            Id = DemoGameId,
            Name = demoGameName,
            VotingSystem = VotingSystem.Fibonacci,
            InviteCode = demoInviteCode,
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
            Id = MasterParticipantId,
            GameId = game.Id,
            UserId = masterUser.Id,
            DisplayName = masterUser.DisplayName,
            Role = ParticipantRole.Master,
            JoinedAt = DateTime.UtcNow,
            IsConnected = true
        };

        var playerParticipant = new GameParticipant
        {
            Id = PlayerParticipantId,
            GameId = game.Id,
            UserId = playerUser.Id,
            DisplayName = playerUser.DisplayName,
            Role = ParticipantRole.Player,
            JoinedAt = DateTime.UtcNow,
            IsConnected = true
        };

        var spectatorParticipant = new GameParticipant
        {
            Id = SpectatorParticipantId,
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
            Id = FirstIssueId,
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
            Id = SecondIssueId,
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
            Id = ThirdIssueId,
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

    private static async Task SeedVotingHistoryAsync(AppDbContext context)
    {
        var historyAlreadyExists = await context.VotingResults
            .AnyAsync(result => result.GameId == DemoGameId);

        if (historyAlreadyExists)
        {
            return;
        }

        var firstIssueExists = await context.Issues.AnyAsync(issue => issue.Id == FirstIssueId);
        var secondIssueExists = await context.Issues.AnyAsync(issue => issue.Id == SecondIssueId);

        if (!firstIssueExists || !secondIssueExists)
        {
            return;
        }

        var votesAlreadyExist = await context.Votes
            .AnyAsync(vote => vote.IssueId == FirstIssueId || vote.IssueId == SecondIssueId);

        if (!votesAlreadyExist)
        {
            await context.Votes.AddRangeAsync(
                new Vote
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1"),
                    IssueId = FirstIssueId,
                    ParticipantId = MasterParticipantId,
                    FinalEstimate = "3",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                    UpdatedAt = DateTime.UtcNow.AddMinutes(-28)
                },
                new Vote
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"),
                    IssueId = FirstIssueId,
                    ParticipantId = PlayerParticipantId,
                    FinalEstimate = "3",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-29),
                    UpdatedAt = DateTime.UtcNow.AddMinutes(-27)
                },
                new Vote
                {
                    Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1"),
                    IssueId = SecondIssueId,
                    ParticipantId = MasterParticipantId,
                    FinalEstimate = "5",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-20),
                    UpdatedAt = DateTime.UtcNow.AddMinutes(-18)
                },
                new Vote
                {
                    Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc2"),
                    IssueId = SecondIssueId,
                    ParticipantId = PlayerParticipantId,
                    FinalEstimate = "8",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-19),
                    UpdatedAt = DateTime.UtcNow.AddMinutes(-17)
                });
        }

        await context.VotingResults.AddRangeAsync(
            new VotingResult
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd01"),
                GameId = DemoGameId,
                IssueId = FirstIssueId,
                FinalEstimate = "3",
                Average = 3,
                Agreement = 100,
                CreatedAt = DateTime.UtcNow.AddMinutes(-25)
            },
            new VotingResult
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd02"),
                GameId = DemoGameId,
                IssueId = SecondIssueId,
                FinalEstimate = "5",
                Average = 6.5,
                Agreement = 50,
                CreatedAt = DateTime.UtcNow.AddMinutes(-15)
            });

        await context.SaveChangesAsync();
    }
}