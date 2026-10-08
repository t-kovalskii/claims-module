using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("Claims");

        builder.Property(c => c.ClaimNumber).IsRequired().HasMaxLength(50);

        // ADR-0008: claim numbers are unique per organisation, not globally.
        builder.HasIndex(c => new { c.OrganizationId, c.ClaimNumber }).IsUnique();

        builder.Property(c => c.PolicyNumber).HasMaxLength(50);
        builder.Property(c => c.ClientName).IsRequired().HasMaxLength(255);

        builder.Property(c => c.Status).IsRequired().HasConversion<string>().HasMaxLength(30);

        builder.Property(c => c.ClosureReason).HasMaxLength(1000);

        // Loose FK to the (non-org-scoped) simulated Policy table, per ADR-0009 — no navigation
        // back, since Claim reads never need to join out to Policy beyond the denormalised
        // PolicyNumber/ClientName captured at intake.
        builder.HasOne<Policy>()
            .WithMany()
            .HasForeignKey(c => c.PolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(c => c.LossEvent)
            .WithOne(l => l.Claim)
            .HasForeignKey<LossEvent>(l => l.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Parties)
            .WithOne(p => p.Claim)
            .HasForeignKey(p => p.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.RiskObjects)
            .WithOne(r => r.Claim)
            .HasForeignKey(r => r.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
