using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;    
using Microsoft.AspNetCore.Identity;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Data;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Game> Games { get; set; }
    public DbSet<GameParticipant> GameParticipants { get; set; }
    public DbSet<Issue> Issues { get; set; }
    public DbSet<Vote> Votes { get; set; }
    public DbSet<VotingHistory> VotingHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<GameParticipant>()
            .Property(x => x.Role)
            .HasConversion<string>();
    }
}