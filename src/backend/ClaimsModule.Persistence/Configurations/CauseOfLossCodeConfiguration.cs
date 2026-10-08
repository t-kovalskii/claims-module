using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class CauseOfLossCodeConfiguration : IEntityTypeConfiguration<CauseOfLossCode>
{
    public void Configure(EntityTypeBuilder<CauseOfLossCode> builder)
    {
        builder.ToTable("CauseOfLossCodes");

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(c => c.Code).IsUnique();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.PerilCategory)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);
    }
}
