using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimNumberCounterConfiguration : IEntityTypeConfiguration<ClaimNumberCounter>
{
    public void Configure(EntityTypeBuilder<ClaimNumberCounter> builder)
    {
        builder.ToTable("ClaimNumberCounters");
        builder.HasKey(c => c.Id);

        // ADR-0008: one counter row per (organisation, year); the unique index is the real
        // safety net against two concurrent first-submissions-of-the-year racing an insert.
        builder.HasIndex(c => new { c.OrganizationId, c.Year }).IsUnique();
    }
}
