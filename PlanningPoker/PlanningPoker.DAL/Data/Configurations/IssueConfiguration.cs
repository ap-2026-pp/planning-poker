using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Data.Configurations;

public class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("Issues");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.FinalEstimate)
            .HasMaxLength(50);

        builder.Property(e => e.IsCurrent)
            .IsRequired();

        builder.Property(e => e.isRemoved)
            .IsRequired();

        builder.HasMany(i => i.Votes)
               .WithOne(v => v.Issue)
               .HasForeignKey(v => v.IssueId);

        builder.HasMany(x => x.VotingHistories)
            .WithOne(x => x.Issue)
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}