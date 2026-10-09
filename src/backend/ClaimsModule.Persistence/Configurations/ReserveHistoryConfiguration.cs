using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ReserveHistoryConfiguration : IEntityTypeConfiguration<ReserveHistory>
{
    public void Configure(EntityTypeBuilder<ReserveHistory> builder)
    {
        builder.ToTable("ReserveHistory");

        builder.Property(h => h.TransactionType).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.ApprovalStatus).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.PostingStatus).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.RejectionReason).HasMaxLength(1000);
        builder.Property(h => h.ChangeReason).HasMaxLength(500);
        builder.Property(h => h.PostingJobId).HasMaxLength(100);

        // ADR-0007 / FRS §6.5: the GL-posting job's idempotency guard (Ticket 08) — the unique
        // index is the real concurrency safety net, not just the job's own pre-check.
        builder.Property(h => h.IdempotencyKey).IsRequired().HasMaxLength(200);
        builder.HasIndex(h => h.IdempotencyKey).IsUnique();

        // Reverse-chronological read per component (FRS §9.6 / Ticket 07's "full transaction
        // history" requirement).
        builder.HasIndex(h => new { h.ReserveComponentId, h.ChangeSequence });
    }
}
