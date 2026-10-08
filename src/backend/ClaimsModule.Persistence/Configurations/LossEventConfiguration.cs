using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class LossEventConfiguration : IEntityTypeConfiguration<LossEvent>
{
    public void Configure(EntityTypeBuilder<LossEvent> builder)
    {
        builder.ToTable("LossEvents");

        // 1:1 with Claim.
        builder.HasIndex(l => l.ClaimId).IsUnique();

        builder.Property(l => l.LossDescription).IsRequired();
        builder.Property(l => l.LossLocation).HasMaxLength(500);
        builder.Property(l => l.PoliceReportNumber).HasMaxLength(100);

        builder.HasOne(l => l.CauseOfLossCode)
            .WithMany()
            .HasForeignKey(l => l.CauseOfLossCodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
