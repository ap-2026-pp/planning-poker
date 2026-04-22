using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Data.Configurations;

public class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("Issues");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Url)
            .HasMaxLength(500);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.IsCurrent)
            .IsRequired();

        builder.Property(x => x.IsRemoved)
            .IsRequired();

        builder.HasOne(x => x.Game)
            .WithMany(x => x.Issues)
            .HasForeignKey(x => x.GameId);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy);

        builder.HasMany(x => x.Votes)
            .WithOne(x => x.Issue)
            .HasForeignKey(x => x.IssueId);

        builder.HasMany(x => x.VotingResults)
            .WithOne(x => x.Issue)
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}