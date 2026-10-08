using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimAuditLogConfiguration : IEntityTypeConfiguration<ClaimAuditLog>
{
    public void Configure(EntityTypeBuilder<ClaimAuditLog> builder)
    {
        builder.ToTable("ClaimAuditLogs");

        builder.Property(a => a.EventType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Description).IsRequired();
        builder.Property(a => a.RelatedEntityType).HasMaxLength(100);

        // Reverse-chronological read per claim (FRS §10.1 GET /api/claims/{id}/audit).
        builder.HasIndex(a => new { a.ClaimId, a.CreatedAt });
    }
}
