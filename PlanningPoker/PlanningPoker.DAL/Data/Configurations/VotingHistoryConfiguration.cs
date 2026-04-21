using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Data.Configurations;

public class VotingHistoryConfiguration : IEntityTypeConfiguration<VotingHistory>
{
    public void Configure(EntityTypeBuilder<VotingHistory> builder)
    {
        builder.ToTable("VotingHistories");
        builder.HasKey(e => e.Id);

        builder.Property(x => x.FinalEstimate)
            .HasMaxLength(50);

        builder.Property(x => x.CreatedAt)
            .IsRequired();
    }
}