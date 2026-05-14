using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Data.Configurations;

public class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> builder)
    {
        builder.ToTable("Votes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Estimate)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Issue)
            .WithMany(x => x.Votes)
            .HasForeignKey(x => x.IssueId);

        builder.HasOne(x => x.Participant)
            .WithMany(x => x.Votes)
            .HasForeignKey(x => x.ParticipantId);

        builder.HasIndex(x => new { x.ParticipantId, x.IssueId })
            .IsUnique();
    }
}