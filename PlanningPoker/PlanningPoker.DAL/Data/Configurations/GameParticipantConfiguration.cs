using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Data.Configurations;

public class GameParticipantConfiguration : IEntityTypeConfiguration<GameParticipant>
{
    public void Configure(EntityTypeBuilder<GameParticipant> builder)
    {
        builder.ToTable("GameParticipants");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.JoinedAt)
            .IsRequired();

        builder.Property(x => x.IsConnected)
            .IsRequired();

        builder.Property(x => x.RemovedAt);

        builder.Property(x => x.Role)
            .IsRequired();

        builder.HasOne(x => x.Game)
            .WithMany(x => x.Participants)
            .HasForeignKey(x => x.GameId);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Participants)
            .HasForeignKey(x => x.UserId);

        builder.HasMany(x => x.Votes)
            .WithOne(x => x.Participant)
            .HasForeignKey(x => x.ParticipantId);
    }
}
