using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("Policies");

        builder.Property(p => p.PolicyNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(p => p.PolicyNumber).IsUnique();

        builder.Property(p => p.ClientName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.EffectiveDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(p => p.ExpirationDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasMany(p => p.Coverages)
            .WithOne(c => c.Policy)
            .HasForeignKey(c => c.PolicyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
