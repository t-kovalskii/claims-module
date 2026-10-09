using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ReserveComponentConfiguration : IEntityTypeConfiguration<ReserveComponent>
{
    public void Configure(EntityTypeBuilder<ReserveComponent> builder)
    {
        // FRS §9.5 names the table ClaimReserveComponents; CONTEXT.md's entity name is the
        // narrower ReserveComponent (a child entity is implicitly claim-scoped already).
        builder.ToTable("ClaimReserveComponents");

        builder.Property(c => c.ComponentType).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.Notes).HasMaxLength(2000);

        // One row per (claim, component type) — a claim never has two independent ledgers for
        // the same component type open simultaneously (ADR-0001/0007's single-aggregate model).
        builder.HasIndex(c => new { c.ClaimId, c.ComponentType }).IsUnique();

        builder.HasMany(c => c.History)
            .WithOne(h => h.ReserveComponent)
            .HasForeignKey(h => h.ReserveComponentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
