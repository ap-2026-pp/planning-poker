using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Data.Configurations;

public class VotingResultConfiguration : IEntityTypeConfiguration<VotingResult>
{
    public void Configure(EntityTypeBuilder<VotingResult> builder)
    {
        builder.ToTable("VotingResults");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FinalEstimate)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Game)
            .WithMany()
            .HasForeignKey(x => x.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Issue)
            .WithMany(x => x.VotingResults)
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}