using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimStatusTransitionConfiguration : IEntityTypeConfiguration<ClaimStatusTransition>
{
    public void Configure(EntityTypeBuilder<ClaimStatusTransition> builder)
    {
        builder.ToTable("ClaimStatusTransitions");

        builder.Property(t => t.FromStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(t => t.ToStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(t => t.RequiredPermission)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(t => new { t.FromStatus, t.ToStatus }).IsUnique();
    }
}
