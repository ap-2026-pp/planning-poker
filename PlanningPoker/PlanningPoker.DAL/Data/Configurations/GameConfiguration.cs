using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Data.Configurations;

public class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("Games");
        builder.HasKey(e => e.Id);
        
        builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);
        
        builder.Property(x => x.VotingSystem)
                .IsRequired()
                .HasMaxLength(50);
        
        builder.Property(x => x.InviteCode)
                .IsRequired()
                .HasMaxLength(50);
        
        builder.Property(x => x.CreatedAt)
                .IsRequired();

        builder.HasMany(g => g.Participants)
               .WithOne(p => p.Game)
               .HasForeignKey(p => p.GameId);
        
        builder.HasMany(g => g.Issues)
               .WithOne(i => i.Game)
               .HasForeignKey(i => i.GameId);
        
        builder.HasMany(g => g.VotingHistories)
               .WithOne(h => h.Game)
               .HasForeignKey(h => h.GameId);
    }
}