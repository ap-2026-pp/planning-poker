using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Infrastructure.Data.Configurations;

public class GameTimerConfiguration : IEntityTypeConfiguration<GameTimer>
{
    public void Configure(EntityTypeBuilder<GameTimer> builder)
    {
        builder.HasKey(t => t.GameId);
        builder.Property(t => t.StartedAt)
            .IsRequired();

        builder.Property(t => t.EndsAt)
            .IsRequired();

        builder.HasOne(t => t.Game)
            .WithOne(g => g.Timer)
            .HasForeignKey<GameTimer>(t => t.GameId)
            .OnDelete(DeleteBehavior.Cascade); 
    }
}