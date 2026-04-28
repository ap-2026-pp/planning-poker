using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Data.Configurations;

public class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("Games");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.Name, x.CreatedBy })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.VotingSystem)
            .IsRequired();

        builder.Property(x => x.InviteCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .IsRequired();

        builder.HasOne(x => x.CreatedByUser)
            .WithMany(x => x.CreatedGames)
            .HasForeignKey(x => x.CreatedBy);

        builder.HasMany(x => x.Participants)
            .WithOne(x => x.Game)
            .HasForeignKey(x => x.GameId);

        builder.HasMany(x => x.Issues)
            .WithOne(x => x.Game)
            .HasForeignKey(x => x.GameId);
    }
}
