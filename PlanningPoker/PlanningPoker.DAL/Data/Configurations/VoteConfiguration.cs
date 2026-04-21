using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Data.Configurations;

public class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> builder)
    {
        builder.ToTable("Vote");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Value)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.HasIndex(i => new { i.ParticipantId, i.IssueId })
            .IsUnique();
    }
}