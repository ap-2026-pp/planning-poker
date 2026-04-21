using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Data.Configurations;

public class GameParticipantConfiguration : IEntityTypeConfiguration<GameParticipant>
{
    public void Configure(EntityTypeBuilder<GameParticipant> builder)
    {
        builder.ToTable("GameParticipants");
        builder.HasKey(e => e.Id);
       
        builder.Property(x => x.DisplayName)
            .IsRequired().HasMaxLength(200);

        builder.Property(x => x.JoinedAt)
            .IsRequired();

        builder.Property(x => x.IsConnected)
            .IsRequired();

        builder.Property(x => x.Role)
            .IsRequired();

        builder.HasMany(p => p.Votes)
               .WithOne(v => v.Participant)
               .HasForeignKey(v => v.ParticipantId);
    }
}