using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class PolicyCoverageConfiguration : IEntityTypeConfiguration<PolicyCoverage>
{
    public void Configure(EntityTypeBuilder<PolicyCoverage> builder)
    {
        builder.ToTable("PolicyCoverages");

        builder.Property(c => c.CoverageType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasIndex(c => new { c.PolicyId, c.CoverageType }).IsUnique();
    }
}
