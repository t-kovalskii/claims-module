using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimRiskObjectConfiguration : IEntityTypeConfiguration<ClaimRiskObject>
{
    public void Configure(EntityTypeBuilder<ClaimRiskObject> builder)
    {
        builder.ToTable("ClaimRiskObjects");

        builder.Property(r => r.AssetType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.AssetDescription).IsRequired().HasMaxLength(500);
        builder.Property(r => r.AssetReference).HasMaxLength(255);

        builder.HasIndex(r => r.ClaimId);
    }
}
