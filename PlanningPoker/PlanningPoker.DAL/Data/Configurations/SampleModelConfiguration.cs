using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Data.Configurations;

public class SampleEntityConfiguration : IEntityTypeConfiguration<SampleModel>
{
    public void Configure(EntityTypeBuilder<SampleModel> builder)
    {
        builder.ToTable("sample_entities");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.CreatedAt).IsRequired();
    }
}